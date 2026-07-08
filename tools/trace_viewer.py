#!/usr/bin/env python3
"""Agent Trace Viewer: parse .jsonl trace → three-pane self-contained HTML.

Pure read-only consumer of JsonlAgentTrace output (C# mod). Zero deps (stdlib only).

Three-pane layout:
  - Left: conversation list (by sessionId, query preview)
  - Mid: ReAct timeline (pre_react steps + per-iter THINKING/tools/ANSWER)
  - Right: detail (messages diff, tools, response, usage)

Navigation: click conversation → click timeline item; prev/next buttons jump by
semantic ReAct round (pre_react → iter0 THINKING → iter0 tools → iter0 ANSWER → ...).

Usage:
    python tools/trace_viewer.py <trace_world_N.jsonl>
    # generates <trace>.html in same dir, opens in default browser
"""
from __future__ import annotations

import html
import json
import sys
import webbrowser
import pathlib
from typing import Any


def parse_trace(path: str) -> tuple[dict[str, dict], list[str], int]:
    """Parse a trace .jsonl file.

    Returns (sessions, order, bad_count):
      - sessions: {sessionId: {"query": str, "events": [event, ...]}}
      - order: list of sessionIds in file order
      - bad_count: number of malformed lines skipped
    """
    sessions: dict[str, dict] = {}
    order: list[str] = []
    bad = 0
    current: str | None = None
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                evt = json.loads(line)
            except json.JSONDecodeError:
                bad += 1
                continue
            sid = evt.get("sessionId")
            etype = evt.get("type")
            if etype == "session_start":
                current = sid
                sessions[sid] = {"query": (evt.get("query") or "")[:30], "events": []}
                order.append(sid)
            if current and current in sessions:
                sessions[current]["events"].append(evt)
            elif current is None and sid and sid in sessions:
                sessions[sid]["events"].append(evt)
    return sessions, order, bad


def build_html(sessions: dict[str, dict], order: list[str], bad_count: int, source_name: str) -> str:
    """Generate the self-contained three-pane HTML."""
    # Left pane: conversation list
    left_items = "".join(
        f'<div class="sess" data-sid="{html.escape(sid)}">{html.escape(sessions[sid]["query"]) or "(empty)"}</div>'
        for sid in order
    )

    # Inline per-session events as JSON
    sessions_json = json.dumps(
        {sid: sessions[sid]["events"] for sid in order},
        ensure_ascii=False,
    )

    bad_banner = f'<div class="banner">⚠️ {bad_count} 行解析失败，已跳过</div>' if bad_count else ""

    return f"""<!DOCTYPE html>
<html lang="zh"><head><meta charset="utf-8"><title>Trace: {html.escape(source_name)}</title>
<style>
* {{ box-sizing: border-box; }}
body {{ background:#1e1e1e; color:#d4d4d4; margin:0; font-family: -apple-system,'Segoe UI',sans-serif; font-size:13px; }}
.banner {{ background:#5a3a1a; color:#ffb; padding:6px 12px; font-size:12px; }}
.container {{ display:flex; height:100vh; }}
.left {{ width:260px; border-right:1px solid #333; overflow-y:auto; flex-shrink:0; }}
.mid {{ width:300px; border-right:1px solid #333; overflow-y:auto; flex-shrink:0; }}
.right {{ flex:1; overflow-y:auto; padding:14px; }}
.sess {{ padding:8px 12px; cursor:pointer; border-bottom:1px solid #2a2a2a; word-break:break-all; }}
.sess:hover {{ background:#2a2a2a; }}
.sess.sel {{ background:#264f78; }}
.tl-phase {{ color:#888; font-size:11px; padding:6px 12px 3px; border-top:1px solid #333; text-transform:uppercase; }}
.tl-item {{ padding:5px 12px; cursor:pointer; border-bottom:1px solid #222; }}
.tl-item:hover {{ background:#2a2a2a; }}
.tl-item.sel {{ background:#264f78; }}
.tl-item .ico {{ display:inline-block; width:18px; }}
.detail-h {{ color:#569cd6; font-weight:bold; margin:8px 0 4px; }}
.detail-sub {{ color:#888; font-size:11px; margin-bottom:8px; }}
.msg {{ background:#2a2a2a; padding:8px 10px; margin:4px 0; border-radius:4px; border-left:3px solid #555; word-break:break-word; }}
.msg.new {{ border-left-color:#4ec9b0; }}
.msg.old {{ opacity:0.5; }}
.msg .role {{ color:#c586c0; font-size:11px; }}
.msg pre {{ white-space:pre-wrap; word-wrap:break-word; margin:4px 0 0; font-size:12px; }}
.usage {{ color:#569cd6; font-size:12px; margin:6px 0; }}
.tool-box {{ background:#2a2a2a; padding:8px; margin:4px 0; border-radius:4px; }}
.tool-box pre {{ white-space:pre-wrap; word-wrap:break-word; font-size:11px; color:#9cdcfe; }}
.toolbar {{ position:sticky; top:0; background:#1e1e1e; padding:8px 0; border-bottom:1px solid #333; margin-bottom:8px; z-index:1; }}
button {{ background:#264f78; color:#d4d4d4; border:none; padding:6px 14px; cursor:pointer; border-radius:3px; font-size:12px; }}
button:hover {{ background:#3678c8; }}
button:disabled {{ opacity:0.4; cursor:default; }}
.empty {{ color:#666; padding:40px; text-align:center; }}
</style></head><body>
{bad_banner}
<div class="container">
  <div class="left">{left_items or '<div class="empty">无对话</div>'}</div>
  <div class="mid" id="timeline"><div class="empty">选择左侧对话</div></div>
  <div class="right" id="detail"><div class="empty">选择时间线项查看详情</div></div>
</div>
<script>
var SESSIONS = {sessions_json};
var ORDER = {json.dumps(order)};
var selSid = null, tlItems = [], selIdx = -1;
var LAST_MSGS = [];

document.querySelectorAll('.sess').forEach(function(el){{
  el.onclick = function(){{ selectSession(el.dataset.sid); }};
}});

function esc(s){{ return (s==null?'':String(s)).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }}

function selectSession(sid){{
  selSid = sid;
  document.querySelectorAll('.sess').forEach(function(e){{
    e.className = e.dataset.sid === sid ? 'sess sel' : 'sess';
  }});
  LAST_MSGS = [];
  renderTimeline();
}}

function renderTimeline(){{
  if(!selSid || !SESSIONS[selSid]){{ document.getElementById('timeline').innerHTML='<div class="empty">无事件</div>'; return; }}
  var evts = SESSIONS[selSid];
  var h = '';
  tlItems = [];
  for(var i=0;i<evts.length;i++){{
    var e = evts[i];
    var t = e.type;
    if(t === 'session_start'){{ h += '<div class="tl-phase">session start</div>'; tlItems.push(i); }}
    else if(t === 'context_step'){{ h += '<div class="tl-phase">'+esc(e.step)+'</div>'; tlItems.push(i); }}
    else if(t === 'llm_call'){{ h += '<div class="tl-item" data-i="'+i+'" onclick="sel('+i+')"><span class="ico">🧠</span>'+esc(e.role)+' '+esc(e.trigger)+'</div>'; tlItems.push(i); }}
    else if(t === 'llm_response'){{ h += '<div class="tl-item" data-i="'+i+'" onclick="sel('+i+')"><span class="ico">💬</span>resp</div>'; tlItems.push(i); }}
    else if(t === 'tool_call'){{ h += '<div class="tl-item" data-i="'+i+'" onclick="sel('+i+')"><span class="ico">🔧</span>'+esc(e.name)+'</div>'; tlItems.push(i); }}
    else if(t === 'tool_result'){{ h += '<div class="tl-item" data-i="'+i+'" onclick="sel('+i+')"><span class="ico">📦</span>res</div>'; tlItems.push(i); }}
    else if(t === 'session_end'){{ h += '<div class="tl-phase">end</div>'; tlItems.push(i); }}
  }}
  document.getElementById('timeline').innerHTML = h;
  if(tlItems.length) sel(tlItems[0]);
}}

function diffPrefix(prev, curr){{
  var n = 0;
  var p = prev || [], c = curr || [];
  for(var i=0;i<p.length && i<c.length;i++){{
    if((p[i].content||'') === (c[i].content||'') && (p[i].role||'') === (c[i].role||'')) n++;
    else break;
  }}
  return n;
}}

function sel(i){{
  selIdx = tlItems.indexOf(i);
  document.querySelectorAll('.tl-item').forEach(function(e){{
    e.className = parseInt(e.dataset.i) === i ? 'tl-item sel' : 'tl-item';
  }});
  var e = SESSIONS[selSid][i];
  var d = '<div class="toolbar"><button onclick="prev()" id="btnPrev">◀ 上一步</button> <button onclick="next()" id="btnNext">下一步 ▶</button></div>';
  var t = e.type;
  if(t === 'llm_call'){{
    var msgs = e.messages || [];
    var common = diffPrefix(LAST_MSGS, msgs);
    d += '<div class="detail-h">LLM Call · '+esc(e.role)+' · '+esc(e.trigger)+'</div>';
    d += '<div class="detail-sub">messages: '+msgs.length+' · tools: '+(e.toolsCount||0)+' · iter: '+(e.iteration!=null?e.iteration:'-')+'</div>';
    if(common > 0) d += '<div class="msg old">['+common+' msgs 与上轮相同，已折叠]</div>';
    for(var j=common;j<msgs.length;j++){{
      var m = msgs[j];
      d += '<div class="msg new"><span class="role">'+esc(m.role)+'</span><pre>'+esc(m.content)+'</pre></div>';
    }}
    if(e.tools){{ d += '<div class="detail-h">Tools</div><div class="tool-box"><pre>'+esc(JSON.stringify(e.tools, null, 2))+'</pre></div>'; }}
    LAST_MSGS = msgs;
  }} else if(t === 'llm_response'){{
    d += '<div class="detail-h">LLM Response · '+esc(e.role)+'</div>';
    d += '<div class="msg"><pre>'+esc(e.content||'(empty)')+'</pre></div>';
    if(e.toolCalls){{ d += '<div class="detail-h">Tool Calls</div><div class="tool-box"><pre>'+esc(JSON.stringify(e.toolCalls, null, 2))+'</pre></div>'; }}
    if(e.usage){{ d += '<div class="usage">↑ '+e.usage.promptTokens+' · ↓ '+e.usage.completionTokens+' · cache '+e.usage.cacheHitTokens+'</div>'; }}
    d += '<div class="detail-sub">finish: '+esc(e.finishReason||'')+' · '+e.durationMs+'ms</div>';
  }} else if(t === 'tool_call'){{
    d += '<div class="detail-h">Tool Call · '+esc(e.name)+'</div>';
    d += '<div class="detail-sub">callId: '+esc(e.callId)+' · iter: '+(e.iteration!=null?e.iteration:'-')+'</div>';
    d += '<div class="tool-box"><pre>'+esc(JSON.stringify(e.args, null, 2))+'</pre></div>';
  }} else if(t === 'tool_result'){{
    d += '<div class="detail-h">Tool Result · '+esc(e.name)+'</div>';
    d += '<div class="detail-sub">callId: '+esc(e.callId)+' · '+e.contentChars+' chars</div>';
    d += '<div class="msg"><pre>'+esc(e.content)+'</pre></div>';
  }} else if(t === 'context_step'){{
    d += '<div class="detail-h">'+esc(e.step)+'</div>';
    d += '<div class="detail-sub">'+e.durationMs+'ms</div>';
    d += '<div class="tool-box"><pre>'+esc(JSON.stringify(e.detail||{{}}, null, 2))+'</pre></div>';
  }} else if(t === 'session_start'){{
    d += '<div class="detail-h">Session Start</div>';
    d += '<div class="detail-sub">worldId: '+e.worldId+' · persona: '+esc(e.personaId||'')+'</div>';
    d += '<div class="msg"><pre>query: '+esc(e.query)+'</pre></div>';
  }} else if(t === 'session_end'){{
    d += '<div class="detail-h">Session End</div>';
    d += '<div class="detail-sub">'+e.thinkingTimeMs+'ms · '+e.totalIterations+' iters · '+e.finalAnswerChars+' chars</div>';
    if(e.tokenUsage){{ d += '<div class="usage">total ↑ '+e.tokenUsage.prompt+' · ↓ '+e.tokenUsage.completion+' · cache '+e.tokenUsage.cacheHit+'</div>'; }}
  }}
  document.getElementById('detail').innerHTML = d;
  updateButtons();
}}

function updateButtons(){{
  var bp = document.getElementById('btnPrev'), bn = document.getElementById('btnNext');
  if(bp) bp.disabled = selIdx <= 0;
  if(bn) bn.disabled = selIdx >= tlItems.length - 1;
}}

function prev(){{ if(selIdx > 0){{ sel(tlItems[selIdx - 1]); }} }}
function next(){{ if(selIdx < tlItems.length - 1){{ sel(tlItems[selIdx + 1]); }} }}

document.addEventListener('keydown', function(e){{
  if(e.key === 'ArrowLeft' || e.key === 'ArrowUp'){{ e.preventDefault(); prev(); }}
  else if(e.key === 'ArrowRight' || e.key === 'ArrowDown'){{ e.preventDefault(); next(); }}
}});
</script>
</body></html>"""


def main():
    if len(sys.argv) < 2:
        print("Usage: python tools/trace_viewer.py <trace_world_N.jsonl>")
        sys.exit(1)
    path = pathlib.Path(sys.argv[1])
    if not path.exists():
        print(f"Error: file not found: {path}", file=sys.stderr)
        sys.exit(1)

    sessions, order, bad = parse_trace(str(path))
    if not order:
        print(f"Error: no sessions found in {path}", file=sys.stderr)
        sys.exit(1)

    html_content = build_html(sessions, order, bad, path.name)
    out = path.with_suffix(".html")
    out.write_text(html_content, encoding="utf-8")
    print(f"Generated: {out}")
    print(f"  sessions: {len(order)}, bad lines: {bad}")
    try:
        webbrowser.open(out.resolve().as_uri())
    except Exception:
        print(f"  open manually: {out}")


if __name__ == "__main__":
    main()
