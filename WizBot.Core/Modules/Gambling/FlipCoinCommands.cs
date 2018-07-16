using Discord;
using Discord.Commands;
using WizBot.Extensions;
using WizBot.Core.Services;
using System.Threading.Tasks;
using WizBot.Common;
using WizBot.Common.Attributes;
using Image = SixLabors.ImageSharp.Image;
using WizBot.Core.Modules.Gambling.Common;
using WizBot.Modules.Gambling.Services;
using WizBot.Core.Common;
using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WizBot.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class FlipCoinCommands : GamblingSubmodule<GamblingService>
        {
            private readonly IImageCache _images;
            private readonly ICurrencyService _cs;
            private readonly DbService _db;
            private static readonly WizBotRandom rng = new WizBotRandom();

            public FlipCoinCommands(IDataCache data, ICurrencyService cs, DbService db)
            {
                _images = data.LocalImages;
                _cs = cs;
                _db = db;
            }

            [WizBotCommand, Usage, Description, Aliases]
            public async Task Flip(int count = 1)
            {
                if (count == 1)
                {
                    var coins = _images.ImageUrls.Coins;
                    if (rng.Next(0, 2) == 1)
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder()
                            .WithOkColor()
                            .WithImageUrl(coins.Heads[rng.Next(0, coins.Heads.Length)].ToString())
                            .WithDescription(Context.User.Mention + " " + GetText("flipped", Format.Bold(GetText("heads"))))).ConfigureAwait(false);

                    }
                    else
                    {
                        await Context.Channel.EmbedAsync(new EmbedBuilder()
                            .WithOkColor()
                            .WithImageUrl(coins.Tails[rng.Next(0, coins.Tails.Length)].ToString())
                            .WithDescription(Context.User.Mention + " " + GetText("flipped", Format.Bold(GetText("tails"))))).ConfigureAwait(false);

                    }
                    return;
                }
                if (count > 10 || count < 1)
                {
                    await ReplyErrorLocalized("flip_invalid", 10).ConfigureAwait(false);
                    return;
                }
                var imgs = new Image<Rgba32>[count];
                for (var i = 0; i < count; i++)
                {
                    using (var heads = _images.Heads[rng.Next(0, _images.Heads.Length)].ToStream())
                    using (var tails = _images.Tails[rng.Next(0, _images.Tails.Length)].ToStream())
                    {
                        if (rng.Next(0, 10) < 5)
                        {
                            imgs[i] = Image.Load(heads);
                        }
                        else
                        {
                            imgs[i] = Image.Load(tails);
                        }
                    }
                }
                using (var img = imgs.Merge())
                using (var stream = img.ToStream())
                {
                    foreach (var i in imgs)
                    {
                        i.Dispose();
                    }

                    await Context.Channel.SendFileAsync(stream, $"{count} coins.png").ConfigureAwait(false);
                }
            }

            public enum BetFlipGuess
            {
                H = 1,
                Head = 1,
                Heads = 1,
                T = 2,
                Tail = 2,
                Tails = 2
            }

            [WizBotCommand, Usage, Description, Aliases]
            public async Task Betflip(ShmartNumber amount, BetFlipGuess guess)
            {
                if (!await CheckBetMandatory(amount).ConfigureAwait(false) || amount == 1)
                    return;

                var removed = await _cs.RemoveAsync(Context.User, "Betflip Gamble", amount, false, gamble: true).ConfigureAwait(false);
                if (!removed)
                {
                    await ReplyErrorLocalized("not_enough", Bc.BotConfig.CurrencyPluralName).ConfigureAwait(false);
                    return;
                }
                BetFlipGuess result;
                Uri imageToSend;
                var coins = _images.ImageUrls.Coins;
                if (rng.Next(0, 2) == 1)
                {
                    imageToSend = coins.Heads[rng.Next(0, coins.Heads.Length)];
                    result = BetFlipGuess.Heads;
                }
                else
                {
                    imageToSend = coins.Tails[rng.Next(0, coins.Tails.Length)];
                    result = BetFlipGuess.Tails;
                }

                string str;
                if (guess == result)
                {
                    var toWin = (long)(amount * Bc.BotConfig.BetflipMultiplier);
                    str = Format.Bold(Context.User.ToString()) + " " + GetText("flip_guess", toWin + Bc.BotConfig.CurrencySign);
                    await _cs.AddAsync(Context.User, "Betflip Gamble", toWin, false, gamble: true).ConfigureAwait(false);
                }
                else
                {
                    str = Context.User.Mention + " " + GetText("better_luck");
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder()
                    .WithDescription(str)
                    .WithOkColor()
                    .WithImageUrl(imageToSend.ToString())).ConfigureAwait(false);
            }
        }
    }
}