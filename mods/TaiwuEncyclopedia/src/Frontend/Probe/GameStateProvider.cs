using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameData.Domains.CombatSkill;
using GameData.Serializer;
using GameData.Utilities;
using TaiwuEncyclopedia.Core.Probe;
using TaiwuEncyclopedia.Core.Probe.Dto;
using UnityEngine;

namespace TaiwuEncyclopedia;

/// <summary>IGameStateProvider 实现。用游戏原生 AsyncCall 读功法, TCS 桥接 Task。
/// 参考 jianghu-youling NpcSnapshotReader FetchSkills + 反编译确认的 GetCombatSkillDisplayData。</summary>
public sealed class GameStateProvider : IGameStateProvider
{
    private const float TIMEOUT = 10f;

    public Task<CombatSkillsSnapshot> GetCombatSkills(IProbeErrorCollector collector)
    {
        var tcs = new TaskCompletionSource<CombatSkillsSnapshot>();
        ProbeDriver.Instance.StartCoroutine(FetchCombatSkillsCoroutine(tcs, collector));
        return tcs.Task;
    }

    private IEnumerator FetchCombatSkillsCoroutine(
        TaskCompletionSource<CombatSkillsSnapshot> tcs, IProbeErrorCollector collector)
    {
        var snap = new CombatSkillsSnapshot();
        var errors = new List<string>();

        // 1. 太吾 charId(同步)
        int taiwuId = -1;
        try { taiwuId = SingletonObject.getInstance<BasicGameData>().TaiwuCharId; }
        catch (Exception e) { errors.Add("TaiwuCharId: " + e.Message); }

        if (taiwuId <= 0)
        {
            snap.Errors = errors.ToArray();
            tcs.TrySetResult(snap);
            yield break;
        }

        // 2. 已学功法 templateId 列表 (P-CS-001)
        List<short> templateIds = null!;
        bool done1 = false;
        try
        {
            CombatSkillDomainMethod.AsyncCall.GetLearnedCombatSkillByType(
                null, taiwuId, (sbyte)(-1),
                (offset, pool) =>
                {
                    try { Serializer.Deserialize(pool, offset, ref templateIds); }
                    catch (Exception e) { errors.Add("GetLearnedCombatSkillByType deser: " + e.Message); }
                    finally { done1 = true; }
                });
        }
        catch (Exception e) { errors.Add("GetLearnedCombatSkillByType: " + e.Message); done1 = true; }
        yield return WaitDone(() => done1);

        if (templateIds == null || templateIds.Count == 0)
        {
            // 字段级失败: 记 collector (路3 由 ProbeBase.TryRead 做, 这里 templateId 读不到属探针级, 直接 unavailable 由 tool 层兜)
            // 但 GetCombatSkills 是纯数据返回, 探针级判断在 ProbeToolBase 的 BuildResult 看 collector
            collector.AddFailed("GetLearnedCombatSkillByType", "P-CS-001",
                new InvalidOperationException("templateIds empty or null"));
            snap.Errors = errors.ToArray();
            tcs.TrySetResult(snap);
            yield break;
        }

        // 3. 批量取显示数据 (P-CS-002)
        List<CombatSkillDisplayData> displayData = null!;
        bool done2 = false;
        try
        {
            CombatSkillDomainMethod.AsyncCall.GetCombatSkillDisplayData(
                null, taiwuId, templateIds,
                (offset, pool) =>
                {
                    try { Serializer.Deserialize(pool, offset, ref displayData); }
                    catch (Exception e) { errors.Add("GetCombatSkillDisplayData deser: " + e.Message); }
                    finally { done2 = true; }
                });
        }
        catch (Exception e) { errors.Add("GetCombatSkillDisplayData: " + e.Message); done2 = true; }
        yield return WaitDone(() => done2);

        // 4. 映射 DisplayData -> LearnedSkillRaw
        // 注意: 协程(IEnumerator)内不能 await。ProbeBase.TryRead 供 Task 型探针用;
        // combat_skills 是协程型, 字段级失败在此用 collector.AddFailed 直接记录(等价于 TryRead 的 catch 分支)。
        if (displayData == null)
        {
            // GetCombatSkillDisplayData 整体失败 -> 字段级 degrade 记 P-CS-002
            collector.AddFailed("GetCombatSkillDisplayData", "P-CS-002",
                new InvalidOperationException("displayData null after AsyncCall"));
        }
        else
        {
            var learned = new List<LearnedSkillRaw>();
            foreach (var d in displayData)
            {
                if (d == null) continue;
                // DisplayData 字段是值拷贝, 不会抛; 唯一风险 SkillConfig null, 用 ?. 兜底
                string name = "";
                try { name = d.SkillConfig?.Name ?? ""; }
                catch (Exception e) { errors.Add("SkillConfig.Name: " + e.Message); }
                short grade = 0;
                try { grade = d.SkillConfig?.Grade ?? (short)0; }
                catch (Exception e) { errors.Add("SkillConfig.Grade: " + e.Message); }
                // Type 是 IFilterableCombatSkill 的显式接口实现, 需 cast 到接口访问
                int skillType = 0;
                try { skillType = ((IFilterableCombatSkill)d).Type; }
                catch (Exception e) { errors.Add("IFilterableCombatSkill.Type: " + e.Message); }
                learned.Add(new LearnedSkillRaw
                {
                    TemplateId = d.TemplateId,
                    Name = name,
                    GradeRaw = grade,
                    SkillTypeRaw = skillType,
                    PracticeLevel = d.PracticeLevel,
                    IsPositive = !d.Revoked,
                    IsReverse = d.Revoked,
                    ReadingStateRaw = d.ReadingState,
                    Power = d.Power,
                    MaxPower = d.MaxPower,
                    Mastered = d.Mastered,
                });
            }
            snap.Learned = learned.ToArray();
        }
        // ReadingState 位翻译(Frontend, 依赖游戏DLL)。Core 翻译(Grade/Type)由 tool 调。
        try { ProbeReadingStateTranslator.Translate(snap); }
        catch (Exception e) { errors.Add("ReadingState translate: " + e.Message); }
        snap.Errors = errors.ToArray();
        tcs.TrySetResult(snap);
    }

    private IEnumerator WaitDone(System.Func<bool> ready)
    {
        float dl = Time.realtimeSinceStartup + TIMEOUT;
        yield return new WaitUntil(() => ready() || Time.realtimeSinceStartup >= dl);
    }
}
