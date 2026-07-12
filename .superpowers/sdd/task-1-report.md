# Task 1 Report: PromptBuilder Split

## Status: DONE

## Commits
- `848c673` feat: split PromptBuilder into BuildThinkPrompt + BuildFinalPrompt, extract RAG strategy from answer rules

## Tests
- PromptBuilderTest: 12 passed / 12 total, 0 failures
  - 7 existing BuildSystemPrompt tests: all pass (method preserved)
  - 5 new tests: all pass
    - BuildThinkPrompt_ContainsToolsAndKnowledge_ButNotPersona
    - BuildThinkPrompt_ContainsRagStrategy_ButNotAnswerRules
    - BuildFinalPrompt_ContainsPersonaAndRules_ButNotTools
    - BuildFinalPrompt_CachesByPersonaId
    - BuildThinkPrompt_ShouldNotCache_BecauseStatic
- Full Core regression: 249 passed / 254 total, 5 failures
  - 5 failures are all in ContextManagerTest (from Task 2 parallel work, not caused by this change)
- Frontend build: 0 errors

## What Was Implemented

### 1. `BuildThinkPrompt()` method
- **File:** `mods/TaiwuEncyclopedia/src/Core/Agent/PromptBuilder.cs`
- Returns cached think prompt with: minimal retriever identity, tool spec, guidance index, RAG strategy section (extracted from answer rules), overview
- Does NOT include: persona, info processing rules, player protection rules, output style
- Uses separate `_cachedThink` field so it doesn't interfere with `BuildFinalPrompt` cache

### 2. `BuildFinalPrompt(string? personaId)` method
- **File:** `mods/TaiwuEncyclopedia/src/Core/Agent/PromptBuilder.cs`
- Returns cached final prompt with: persona, info processing section, player protection section, output style
- Does NOT include: tool spec, overview, reading strategy
- Reuses `_cached` / `_cachedPersonaId` fields for personaId-based caching

### 3. `ExtractSection(string markdown, string sectionName)` helper
- **File:** `mods/TaiwuEncyclopedia/src/Core/Agent/PromptBuilder.cs`
- Private static method that extracts content under a `## SectionName` header up to the next `##` header or end of text
- Used by both `BuildThinkPrompt` (to extract RAG strategy) and `BuildFinalPrompt` (to extract info processing and player protection)

### 4. `BuildSystemPrompt()` preserved
- Not removed, not modified. All 7 existing tests still pass.

### 5. Tests
- **File:** `tests/TaiwuEncyclopedia.Core.Tests/Agent/PromptBuilderTest.cs`
- Updated `MakeSm()` to include structured answer-rules.md with `## 信息处理`, `## 玩家保护`, `## RAG 检索策略` sections
- Added 5 new test methods verifying content partition between think and final prompts

## Produced Interface
- `PromptBuilder.BuildThinkPrompt()` -> `string` (static content, cached in `_cachedThink`)
- `PromptBuilder.BuildFinalPrompt(string? personaId = null)` -> `string` (persona-dependent, cached by personaId)
- `PromptBuilder.BuildSystemPrompt(string? personaId = null)` -> `string` (preserved, deprecated for Task 3)

## Concerns
- `BuildFinalPrompt` reuses the `_cached` / `_cachedPersonaId` fields originally used by `BuildSystemPrompt`. Calling both methods with the same personaId will share cache. This is intentional since `BuildSystemPrompt` will be deprecated once Task 3 wires up the new methods.
- The 5 ContextManagerTest failures are pre-existing from Task 2 (parallel branch) and are not caused by this change.

## Self-review
- Verified `BuildSystemPrompt` still compiles and all 7 original tests pass (no removal)
- Verified `BuildThinkPrompt` contains: tool spec, guidance index, RAG strategy section, overview -- but NOT persona, answer rules (info/protect), or output style
- Verified `BuildFinalPrompt` contains: persona, info processing, player protection, output style -- but NOT tool spec, overview, or reading strategy
- Verified `ExtractSection` correctly parses `## Section` headers from markdown, skipping the rest of the header line
- Verified `BuildThinkPrompt` uses `_cachedThink` (separate from `_cached`) so the two prompt types don't interfere
- Verified `BuildFinalPrompt` reuses `_cached`/`_cachedPersonaId` for personaId-based caching
- Verified Frontend build succeeds (0 errors)
- Did NOT modify SkillManager (per task constraints)

## Post-review Bug Fix

### Bug: BuildFinalPrompt cache collision with BuildSystemPrompt

**Problem:** `BuildFinalPrompt` reused `_cached` / `_cachedPersonaId` -- the same fields used by `BuildSystemPrompt`. If someone called `BuildSystemPrompt("sword-will")` then `BuildFinalPrompt("sword-will")`, the second call returned the `BuildSystemPrompt` result (which includes tool specs + overview that should NOT be in the Final prompt).

**Fix:** Added dedicated cache fields `_cachedFinal` / `_cachedFinalPersonaId` for `BuildFinalPrompt`, so the two methods have independent caches.

### Minor: Removed persona instruction from _toolSpec

**Problem:** `_toolSpec` contained `- 最终回答时以选中 persona 的口吻给出。` which contradicts the Think prompt's "no persona" design (Think stage should not reference persona at all).

**Fix:** Removed the line from `_toolSpec`. `BuildFinalPrompt` already includes the persona section, so the instruction is redundant there.

### Minor: Stale comments in BuildThinkPrompt

**Status:** The two stale comment lines referenced in the review (`// 3. 百晓册阅读策略（同 _toolSpec 中的「百晓册阅读策略」段，此处不再重复）` and `// _toolSpec 已包含百晓册阅读策略 + RAG 检索策略引用`) do not exist in the current file. They were likely from an earlier draft and already removed. No change needed.

### Test Results
- PromptBuilder tests: 12 passed / 12 total, 0 failures
- Full Core regression: 254 passed / 254 total, 0 failures
