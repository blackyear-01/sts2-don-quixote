using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 「堕落 X」关键词的接口：堕落值达到 <see cref="DonQuixote_CorruptAt"/> 时，
/// 这张牌无需打出就会被自动替换成 <see cref="DonQuixote_CorruptedVersion"/> 返回的堕落版本。
/// </summary>
public interface IDonQuixoteCorruptible
{
    /// <summary>触发腐化所需的堕落值。</summary>
    int DonQuixote_CorruptAt { get; }

    /// <summary>为指定玩家创建对应的堕落版本卡牌实例。</summary>
    CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner);

    /// <summary>
    /// 堕落版本的「规范实例」，只用于悬停说明里把堕落卡显示在原卡旁边
    /// （和原版「先制打击」悬停时显示「小刀」一样）。
    /// 不能拿它进战斗，真正入池要用 <see cref="DonQuixote_CorruptedVersion"/>。
    /// </summary>
    CardModel DonQuixote_CorruptedCanonical { get; }
}

/// <summary>
/// 标记接口：堕落版本的卡（衍生卡）。这类卡使用「卡框 2」，且不会出现在卡池里。
/// </summary>
/// <summary>衍生卡标记：只能由其它效果生成，不进入卡池奖励与图鉴。</summary>
public interface IDonQuixoteTokenCard
{
}

public interface IDonQuixoteCorruptedCard
{
}

/// <summary>
/// 「在卡池里，但不可通过选卡获得」的卡（文档里写作「卡池：堂吉诃德（不可通过选卡获得）」）。
/// 例如只有遗物才会发放的「幸福笑容如鲜花般绽放之地」。
/// 和 <see cref="IDonQuixoteTokenCard"/> 的区别：这类卡仍然会出现在卡牌图鉴里，
/// 只是不会出现在战斗奖励 / 商店等随机选卡池里。
/// </summary>
public interface IDonQuixoteUnselectableCard
{
}
