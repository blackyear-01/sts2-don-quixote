using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 「堕落 X」关键词的接口：堕落值达到 <see cref="DonQuixote_CorruptAt"/> 时。
/// </summary>
public interface IDonQuixoteCorruptible
{
    /// <summary>
    /// 触发腐化所需的堕落值。
    /// </summary>
    int DonQuixote_CorruptAt { get; }

    /// <summary>
    /// 为指定玩家创建对应的堕落版本卡牌实例。
    /// </summary>
    CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner);

    /// <summary>
    /// 堕落版本的「规范实例」。
    /// </summary>
    CardModel DonQuixote_CorruptedCanonical { get; }
}

/// <summary>
/// 衍生卡标记：只能由其它效果生成，不进入卡池奖励与图鉴。
/// </summary>
public interface IDonQuixoteTokenCard
{
}

/// <summary>
/// 标记接口：堕落版本的卡（衍生卡）。
/// </summary>
public interface IDonQuixoteCorruptedCard
{
}

/// <summary>
/// 「在卡池里，但不可通过选卡获得」的卡（文档里写作「卡池：堂吉诃德（不可通过选卡获得）」）。
/// </summary>
public interface IDonQuixoteUnselectableCard
{
}
