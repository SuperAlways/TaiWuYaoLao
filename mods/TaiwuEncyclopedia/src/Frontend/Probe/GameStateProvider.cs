using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameData.Domains.Character;
using GameData.Domains.Character.Display;
using GameData.Domains.CombatSkill;
using GameData.Domains.Taiwu;
using GameData.Domains.Taiwu.Display;
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

    public Task<TaiwuSnapshot> GetTaiwu(IProbeErrorCollector collector)
    {
        var tcs = new TaskCompletionSource<TaiwuSnapshot>();
        ProbeDriver.Instance.StartCoroutine(FetchTaiwuCoroutine(tcs, collector));
        return tcs.Task;
    }

    public Task<NpcSnapshot> GetNpcDetail(int charId, IProbeErrorCollector collector)
    {
        var tcs = new TaskCompletionSource<NpcSnapshot>();
        ProbeDriver.Instance.StartCoroutine(FetchNpcCoroutine(charId, tcs, collector));
        return tcs.Task;
    }

    public Task<InventorySnapshot> GetInventory(int charId, IProbeErrorCollector collector)
    {
        var tcs = new TaskCompletionSource<InventorySnapshot>();
        ProbeDriver.Instance.StartCoroutine(FetchInventoryCoroutine(charId, tcs, collector));
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
        // 翻译(Frontend ProbeTranslator, 全 20 项)
        try { ProbeTranslator.Translate(snap); }
        catch (Exception e) { errors.Add("Translate: " + e.Message); }
        snap.Errors = errors.ToArray();
        tcs.TrySetResult(snap);
    }

    private IEnumerator FetchTaiwuCoroutine(
        TaskCompletionSource<TaiwuSnapshot> tcs, IProbeErrorCollector collector)
    {
        var snap = new TaiwuSnapshot();
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

        snap.CharId = taiwuId;

        // 2. 画像层: GetCharacterDisplayData (P-TW-001)
        CharacterDisplayData? dd = null;
        bool done1 = false;
        try
        {
            CharacterDomainMethod.AsyncCall.GetCharacterDisplayData(
                null, taiwuId,
                (offset, pool) =>
                {
                    try { Serializer.Deserialize(pool, offset, ref dd); }
                    catch (Exception e) { errors.Add("GetCharacterDisplayData deser: " + e.Message); }
                    finally { done1 = true; }
                });
        }
        catch (Exception e) { errors.Add("GetCharacterDisplayData: " + e.Message); done1 = true; }
        yield return WaitDone(() => done1);

        if (dd != null)
        {
            try { snap.Name = NameCenter.GetMonasticTitleOrDisplayName(dd, false) ?? ""; }
            catch (Exception e) { errors.Add("NameCenter.GetMonasticTitleOrDisplayName: " + e.Message); }
            snap.GenderRaw = dd.Gender;
            snap.Age = dd.ActualAge;
            snap.StanceRaw = dd.BehaviorType;
            snap.SectTemplateId = dd.OrgInfo.OrgTemplateId;
            snap.GradeRaw = dd.OrgInfo.Grade;
            snap.ConsummateLevel = dd.ConsummateLevel;
            snap.Charm = dd.Charm;
            snap.Alertness = dd.Alertness;
            snap.FeatureIds = dd.FeatureIds?.Select(x => (int)x).ToArray() ?? Array.Empty<int>();
            snap.Health = dd.Health;
            snap.MaxHealth = dd.LeftMaxHealth;
            snap.Happiness = dd.Happiness;
            snap.Fame = dd.FameType;
            snap.Personalities = new sbyte[7];
            try
            {
                var personalities = dd.Personalities;
                for (int i = 0; i < 7; i++)
                    snap.Personalities[i] = personalities[i];
            }
            catch (Exception e) { errors.Add("Personalities: " + e.Message); }
            snap.AliveState = dd.AliveState;
            snap.CompletelyInfected = dd.CompletelyInfected;
            snap.InfluencePower = dd.InfluencePower;
            snap.LocationText = ProbeTranslator.ResolveLocationText(dd.Location);
        }
        else
        {
            collector.AddFailed("GetCharacterDisplayData", "P-TW-001",
                new InvalidOperationException("dd null after AsyncCall"));
        }

        // 3. 内力层: RequestTaiwuNeiliProportionDisplayData (P-TW-002)
        TaiwuNeiliProportionDisplayData? neili = null;
        bool done2 = false;
        try
        {
            TaiwuDomainMethod.AsyncCall.RequestTaiwuNeiliProportionDisplayData(
                null,
                (offset, pool) =>
                {
                    try { Serializer.Deserialize(pool, offset, ref neili); }
                    catch (Exception e) { errors.Add("RequestTaiwuNeiliProportionDisplayData deser: " + e.Message); }
                    finally { done2 = true; }
                });
        }
        catch (Exception e) { errors.Add("RequestTaiwuNeiliProportionDisplayData: " + e.Message); done2 = true; }
        yield return WaitDone(() => done2);

        if (neili != null)
        {
            snap.NeiliTypeRaw = neili.DestType;
            snap.FiveElementsProportion = new int[5];
            try
            {
                var proportion = neili.NeiliProportion;
                for (int i = 0; i < 5; i++)
                    snap.FiveElementsProportion[i] = proportion[i];
            }
            catch (Exception e) { errors.Add("NeiliProportion: " + e.Message); }
            try
            {
                var neiliType = Config.NeiliType.Instance[neili.DestType];
                snap.FiveElementIndex = neiliType?.FiveElements ?? 0;
            }
            catch (Exception e) { errors.Add("Config.NeiliType: " + e.Message); }
        }
        else
        {
            collector.AddFailed("RequestTaiwuNeiliProportionDisplayData", "P-TW-002",
                new InvalidOperationException("neili null after AsyncCall"));
        }

        // 4. 属性层: GetCharacterAttributeDisplayData (P-TW-003)
        CharacterAttributeDisplayData? attr = null;
        bool done3 = false;
        try
        {
            CharacterDomainMethod.AsyncCall.GetCharacterAttributeDisplayData(
                null, taiwuId,
                (offset, pool) =>
                {
                    try { Serializer.Deserialize(pool, offset, ref attr); }
                    catch (Exception e) { errors.Add("GetCharacterAttributeDisplayData deser: " + e.Message); }
                    finally { done3 = true; }
                });
        }
        catch (Exception e) { errors.Add("GetCharacterAttributeDisplayData: " + e.Message); done3 = true; }
        yield return WaitDone(() => done3);

        if (attr != null)
        {
            snap.CurMainAttributes = new short[6];
            snap.MaxMainAttributes = new short[6];
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    snap.CurMainAttributes[i] = attr.CurMainAttributes[i];
                    snap.MaxMainAttributes[i] = attr.MaxMainAttributes[i];
                }
            }
            catch (Exception e) { errors.Add("MainAttributes: " + e.Message); }
            try { snap.AtkHitOuter = attr.AtkHitAttribute[0]; snap.AtkHitInner = attr.AtkHitAttribute[1]; }
            catch (Exception e) { errors.Add("AtkHitAttribute: " + e.Message); }
            try { snap.AtkPenetrateOuter = attr.AtkPenetrability.Outer; snap.AtkPenetrateInner = attr.AtkPenetrability.Inner; }
            catch (Exception e) { errors.Add("AtkPenetrability: " + e.Message); }
            try { snap.DefHitOuter = attr.DefHitAttribute[0]; snap.DefHitInner = attr.DefHitAttribute[1]; }
            catch (Exception e) { errors.Add("DefHitAttribute: " + e.Message); }
            try { snap.DefPenetrateOuter = attr.DefPenetrability.Outer; snap.DefPenetrateInner = attr.DefPenetrability.Inner; }
            catch (Exception e) { errors.Add("DefPenetrability: " + e.Message); }
            snap.MoveSpeed = attr.MoveSpeed;
            snap.CastSpeed = attr.CastSpeed;
            snap.AttackSpeed = attr.AttackSpeed;
            snap.InnerRatio = attr.InnerRatio;
            // PoisonResists mapping skipped - unknown structure
        }
        else
        {
            collector.AddFailed("GetCharacterAttributeDisplayData", "P-TW-003",
                new InvalidOperationException("attr null after AsyncCall"));
        }

        // 5. 资源层: GetCharacterItemsDisplayData (P-TW-004)
        CharacterItemsDisplayData? items = null;
        bool done4 = false;
        try
        {
            CharacterDomainMethod.AsyncCall.GetCharacterItemsDisplayData(
                null, taiwuId,
                (offset, pool) =>
                {
                    try { Serializer.Deserialize(pool, offset, ref items); }
                    catch (Exception e) { errors.Add("GetCharacterItemsDisplayData deser: " + e.Message); }
                    finally { done4 = true; }
                });
        }
        catch (Exception e) { errors.Add("GetCharacterItemsDisplayData: " + e.Message); done4 = true; }
        yield return WaitDone(() => done4);

        if (items != null)
        {
            snap.Resources = new int[8];
            try
            {
                for (int i = 0; i < 8; i++)
                    snap.Resources[i] = items.Resources[i];
            }
            catch (Exception e) { errors.Add("Resources: " + e.Message); }
        }
        else
        {
            collector.AddFailed("GetCharacterItemsDisplayData", "P-TW-004",
                new InvalidOperationException("items null after AsyncCall"));
        }

        // 6. 资质层: GetCharacterMenuAttainmentDisplayData (P-TW-005)
        CharacterMenuAttainmentDisplayData? att = null;
        bool done5 = false;
        try
        {
            CharacterDomainMethod.AsyncCall.GetCharacterMenuAttainmentDisplayData(
                null, taiwuId,
                (offset, pool) =>
                {
                    try { Serializer.Deserialize(pool, offset, ref att); }
                    catch (Exception e) { errors.Add("GetCharacterMenuAttainmentDisplayData deser: " + e.Message); }
                    finally { done5 = true; }
                });
        }
        catch (Exception e) { errors.Add("GetCharacterMenuAttainmentDisplayData: " + e.Message); done5 = true; }
        yield return WaitDone(() => done5);

        if (att != null)
        {
            snap.CombatSkillQualifications = new short[14];
            snap.CombatSkillAttainments = new short[14];
            try
            {
                for (int i = 0; i < 14; i++)
                {
                    snap.CombatSkillQualifications[i] = att.CombatSkillQualifications[i];
                    snap.CombatSkillAttainments[i] = att.CombatSkillAttainments[i];
                }
            }
            catch (Exception e) { errors.Add("CombatSkillQualifications/Attainments: " + e.Message); }
            snap.CombatSkillGrowthType = att.CombatSkillGrowthType;
            try
            {
                // LifeSkillQualifications/LifeSkillAttainments 需要获取长度
                var lifeQ = new List<short>();
                var lifeA = new List<short>();
                int idx = 0;
                while (true)
                {
                    try
                    {
                        lifeQ.Add(att.LifeSkillQualifications[idx]);
                        lifeA.Add(att.LifeSkillAttainments[idx]);
                        idx++;
                    }
                    catch { break; }
                }
                snap.LifeSkillQualifications = lifeQ.ToArray();
                snap.LifeSkillAttainments = lifeA.ToArray();
            }
            catch (Exception e) { errors.Add("LifeSkillQualifications/Attainments: " + e.Message); }
            snap.LifeSkillGrowthType = att.LifeSkillGrowthType;
            snap.DivinePower = att.DivinePower;
            snap.GhostTechnique = att.GhostTechnique;
        }
        else
        {
            collector.AddFailed("GetCharacterMenuAttainmentDisplayData", "P-TW-005",
                new InvalidOperationException("att null after AsyncCall"));
        }

        // 7. 翻译(Frontend ProbeTranslator)
        try { ProbeTranslator.Translate(snap); }
        catch (Exception e) { errors.Add("Translate: " + e.Message); }

        snap.Errors = errors.ToArray();
        tcs.TrySetResult(snap);
    }

    private IEnumerator FetchNpcCoroutine(
        int charId, TaskCompletionSource<NpcSnapshot> tcs, IProbeErrorCollector collector)
    {
        var snap = new NpcSnapshot();
        var errors = new List<string>();

        snap.CharId = charId;

        if (charId <= 0)
        {
            snap.Errors = errors.ToArray();
            tcs.TrySetResult(snap);
            yield break;
        }

        // 1. 画像层: GetCharacterDisplayData (P-NPC-001)
        CharacterDisplayData? dd = null;
        bool done1 = false;
        try
        {
            CharacterDomainMethod.AsyncCall.GetCharacterDisplayData(
                null, charId,
                (offset, pool) =>
                {
                    try { Serializer.Deserialize(pool, offset, ref dd); }
                    catch (Exception e) { errors.Add("GetCharacterDisplayData deser: " + e.Message); }
                    finally { done1 = true; }
                });
        }
        catch (Exception e) { errors.Add("GetCharacterDisplayData: " + e.Message); done1 = true; }
        yield return WaitDone(() => done1);

        if (dd != null)
        {
            try { snap.Name = NameCenter.GetMonasticTitleOrDisplayName(dd, false) ?? ""; }
            catch (Exception e) { errors.Add("NameCenter.GetMonasticTitleOrDisplayName: " + e.Message); }
            snap.GenderRaw = dd.Gender;
            snap.Age = dd.ActualAge;
            snap.StanceRaw = dd.BehaviorType;
            snap.SectTemplateId = dd.OrgInfo.OrgTemplateId;
            snap.GradeRaw = dd.OrgInfo.Grade;
            snap.ConsummateLevel = dd.ConsummateLevel;
            snap.Charm = dd.Charm;
            snap.Alertness = dd.Alertness;
            snap.FeatureIds = dd.FeatureIds?.Select(x => (int)x).ToArray() ?? Array.Empty<int>();
            snap.Health = dd.Health;
            snap.MaxHealth = dd.LeftMaxHealth;
            snap.Happiness = dd.Happiness;
            snap.Fame = dd.FameType;
            snap.Personalities = new sbyte[7];
            try
            {
                var personalities = dd.Personalities;
                for (int i = 0; i < 7; i++)
                    snap.Personalities[i] = personalities[i];
            }
            catch (Exception e) { errors.Add("Personalities: " + e.Message); }
            snap.AliveState = dd.AliveState;
            snap.CompletelyInfected = dd.CompletelyInfected;
            snap.InfluencePower = dd.InfluencePower;
            snap.LocationText = ProbeTranslator.ResolveLocationText(dd.Location);
            // 额外字段: FavorRaw + RelationBits (带兜底)
            snap.FavorRaw = (dd.FavorabilityToTaiwu != short.MinValue) ? dd.FavorabilityToTaiwu : (short)0;
            snap.RelationBits = (dd.RelationToTaiwu != ushort.MaxValue) ? dd.RelationToTaiwu : (ushort)0;
        }
        else
        {
            collector.AddFailed("GetCharacterDisplayData", "P-NPC-001",
                new InvalidOperationException("dd null after AsyncCall"));
        }

        // 2. 属性层: GetCharacterAttributeDisplayData (P-NPC-002)
        CharacterAttributeDisplayData? attr = null;
        bool done2 = false;
        try
        {
            CharacterDomainMethod.AsyncCall.GetCharacterAttributeDisplayData(
                null, charId,
                (offset, pool) =>
                {
                    try { Serializer.Deserialize(pool, offset, ref attr); }
                    catch (Exception e) { errors.Add("GetCharacterAttributeDisplayData deser: " + e.Message); }
                    finally { done2 = true; }
                });
        }
        catch (Exception e) { errors.Add("GetCharacterAttributeDisplayData: " + e.Message); done2 = true; }
        yield return WaitDone(() => done2);

        if (attr != null)
        {
            snap.CurMainAttributes = new short[6];
            snap.MaxMainAttributes = new short[6];
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    snap.CurMainAttributes[i] = attr.CurMainAttributes[i];
                    snap.MaxMainAttributes[i] = attr.MaxMainAttributes[i];
                }
            }
            catch (Exception e) { errors.Add("MainAttributes: " + e.Message); }
            try { snap.AtkHitOuter = attr.AtkHitAttribute[0]; snap.AtkHitInner = attr.AtkHitAttribute[1]; }
            catch (Exception e) { errors.Add("AtkHitAttribute: " + e.Message); }
            try { snap.AtkPenetrateOuter = attr.AtkPenetrability.Outer; snap.AtkPenetrateInner = attr.AtkPenetrability.Inner; }
            catch (Exception e) { errors.Add("AtkPenetrability: " + e.Message); }
            try { snap.DefHitOuter = attr.DefHitAttribute[0]; snap.DefHitInner = attr.DefHitAttribute[1]; }
            catch (Exception e) { errors.Add("DefHitAttribute: " + e.Message); }
            try { snap.DefPenetrateOuter = attr.DefPenetrability.Outer; snap.DefPenetrateInner = attr.DefPenetrability.Inner; }
            catch (Exception e) { errors.Add("DefPenetrability: " + e.Message); }
            snap.MoveSpeed = attr.MoveSpeed;
            snap.CastSpeed = attr.CastSpeed;
            snap.AttackSpeed = attr.AttackSpeed;
            snap.InnerRatio = attr.InnerRatio;
        }
        else
        {
            collector.AddFailed("GetCharacterAttributeDisplayData", "P-NPC-002",
                new InvalidOperationException("attr null after AsyncCall"));
        }

        // 3. 资质层: GetCharacterMenuAttainmentDisplayData (P-NPC-003)
        CharacterMenuAttainmentDisplayData? att = null;
        bool done3 = false;
        try
        {
            CharacterDomainMethod.AsyncCall.GetCharacterMenuAttainmentDisplayData(
                null, charId,
                (offset, pool) =>
                {
                    try { Serializer.Deserialize(pool, offset, ref att); }
                    catch (Exception e) { errors.Add("GetCharacterMenuAttainmentDisplayData deser: " + e.Message); }
                    finally { done3 = true; }
                });
        }
        catch (Exception e) { errors.Add("GetCharacterMenuAttainmentDisplayData: " + e.Message); done3 = true; }
        yield return WaitDone(() => done3);

        if (att != null)
        {
            snap.CombatSkillQualifications = new short[14];
            snap.CombatSkillAttainments = new short[14];
            try
            {
                for (int i = 0; i < 14; i++)
                {
                    snap.CombatSkillQualifications[i] = att.CombatSkillQualifications[i];
                    snap.CombatSkillAttainments[i] = att.CombatSkillAttainments[i];
                }
            }
            catch (Exception e) { errors.Add("CombatSkillQualifications/Attainments: " + e.Message); }
            snap.CombatSkillGrowthType = att.CombatSkillGrowthType;
            try
            {
                var lifeQ = new List<short>();
                var lifeA = new List<short>();
                int idx = 0;
                while (true)
                {
                    try
                    {
                        lifeQ.Add(att.LifeSkillQualifications[idx]);
                        lifeA.Add(att.LifeSkillAttainments[idx]);
                        idx++;
                    }
                    catch { break; }
                }
                snap.LifeSkillQualifications = lifeQ.ToArray();
                snap.LifeSkillAttainments = lifeA.ToArray();
            }
            catch (Exception e) { errors.Add("LifeSkillQualifications/Attainments: " + e.Message); }
            snap.LifeSkillGrowthType = att.LifeSkillGrowthType;
            snap.DivinePower = att.DivinePower;
            snap.GhostTechnique = att.GhostTechnique;
        }
        else
        {
            collector.AddFailed("GetCharacterMenuAttainmentDisplayData", "P-NPC-003",
                new InvalidOperationException("att null after AsyncCall"));
        }

        // 4. 翻译(Frontend ProbeTranslator)
        try { ProbeTranslator.Translate(snap); }
        catch (Exception e) { errors.Add("Translate: " + e.Message); }

        snap.Errors = errors.ToArray();
        tcs.TrySetResult(snap);
    }

    private IEnumerator FetchInventoryCoroutine(
        int charId, TaskCompletionSource<InventorySnapshot> tcs, IProbeErrorCollector collector)
    {
        var snap = new InventorySnapshot();
        var errors = new List<string>();

        // charId <= 0 时使用太吾 charId
        int targetCharId = charId;
        if (targetCharId <= 0)
        {
            try { targetCharId = SingletonObject.getInstance<BasicGameData>().TaiwuCharId; }
            catch (Exception e) { errors.Add("TaiwuCharId: " + e.Message); }
        }

        if (targetCharId <= 0)
        {
            snap.Errors = errors.ToArray();
            tcs.TrySetResult(snap);
            yield break;
        }

        // 1. GetCharacterItemsDisplayData (P-INV-001)
        CharacterItemsDisplayData? pkg = null;
        bool done1 = false;
        try
        {
            CharacterDomainMethod.AsyncCall.GetCharacterItemsDisplayData(
                null, targetCharId,
                (offset, pool) =>
                {
                    try { Serializer.Deserialize(pool, offset, ref pkg); }
                    catch (Exception e) { errors.Add("GetCharacterItemsDisplayData deser: " + e.Message); }
                    finally { done1 = true; }
                });
        }
        catch (Exception e) { errors.Add("GetCharacterItemsDisplayData: " + e.Message); done1 = true; }
        yield return WaitDone(() => done1);

        var itemsList = new List<InventoryItemRaw>();
        if (pkg != null)
        {
            // Resources
            snap.Resources = new int[8];
            try
            {
                for (int i = 0; i < 8; i++)
                    snap.Resources[i] = pkg.Resources[i];
            }
            catch (Exception e) { errors.Add("Resources: " + e.Message); }

            // InventoryItems
            try
            {
                if (pkg.InventoryItems != null)
                {
                    foreach (var item in pkg.InventoryItems)
                    {
                        if (item == null || item.Amount <= 0) continue;
                        var key = item.RealKey;
                        itemsList.Add(new InventoryItemRaw
                        {
                            Name = "",
                            TemplateId = key.TemplateId,
                            ItemType = key.ItemType,
                            Grade = 0,
                            Amount = item.Amount,
                            ModificationState = key.ModificationState,
                            AllowTrade = false,
                            IsSpecial = false,
                        });
                    }
                }
            }
            catch (Exception e) { errors.Add("InventoryItems: " + e.Message); }
        }
        else
        {
            collector.AddFailed("GetCharacterItemsDisplayData", "P-INV-001",
                new InvalidOperationException("pkg null after AsyncCall"));
        }
        snap.Items = itemsList.ToArray();

        // 2. GetAllEquipmentItems (P-INV-002)
        var equipList = new List<EquipmentRaw>();
        GameData.Domains.Item.ItemKey[]? equipment = null;
        bool done2 = false;
        try
        {
            // 按照 FetchTaiwuCoroutine 的模式尝试 - 先在 CharacterDomainMethod 查找
            // 由于不确定确切方法名，先尝试 GetCharacterEquipDisplayData 或类似命名
            // 先注释掉装备部分，只实现背包物品，后续可扩展装备
            errors.Add("GetAllEquipmentItems: Skipping equipment for now - method lookup pending");
            done2 = true;
        }
        catch (Exception e) { errors.Add("GetAllEquipmentItems: " + e.Message); done2 = true; }

        yield return WaitDone(() => done2);

        // 装备暂时留空，已实现背包物品和资源
        snap.Equipment = equipList.ToArray();

        // 3. 翻译(Frontend ProbeTranslator)
        try { ProbeTranslator.Translate(snap); }
        catch (Exception e) { errors.Add("Translate: " + e.Message); }

        snap.Errors = errors.ToArray();
        tcs.TrySetResult(snap);
    }

    private IEnumerator WaitDone(System.Func<bool> ready)
    {
        float dl = Time.realtimeSinceStartup + TIMEOUT;
        yield return new WaitUntil(() => ready() || Time.realtimeSinceStartup >= dl);
    }
}
