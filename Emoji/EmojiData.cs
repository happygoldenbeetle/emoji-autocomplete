using System;
using System.Collections.Generic;
using System.Linq;

namespace EmojiTyper.Emoji;

/// <summary>
/// A single emoji entry: its Unicode glyph and the shortcode used to type it.
/// </summary>
public readonly record struct EmojiEntry(string Shortcode, string Glyph);

/// <summary>
/// Starter shortcode → emoji dataset (Discord / GitHub style names) plus a
/// prefix-search API used to populate the suggestion popup.
///
/// This is intentionally a curated subset. To ship the full set, replace
/// <see cref="Map"/> with a generated table from emojilib / gemoji, or load
/// one from an embedded JSON resource at startup — the search API below does
/// not care how the dictionary is populated.
/// </summary>
public static class EmojiData
{
    // shortcode (without surrounding colons) -> glyph
    public static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // faces / smileys
            ["grinning"] = "😀",
            ["smile"] = "😄",
            ["smiley"] = "😃",
            ["grin"] = "😁",
            ["laughing"] = "😆",
            ["sweat_smile"] = "😅",
            ["rofl"] = "🤣",
            ["joy"] = "😂",
            ["slightly_smiling_face"] = "🙂",
            ["upside_down_face"] = "🙃",
            ["wink"] = "😉",
            ["blush"] = "😊",
            ["innocent"] = "😇",
            ["smiling_face_with_three_hearts"] = "🥰",
            ["heart_eyes"] = "😍",
            ["star_struck"] = "🤩",
            ["kissing_heart"] = "😘",
            ["kissing"] = "😗",
            ["yum"] = "😋",
            ["stuck_out_tongue"] = "😛",
            ["stuck_out_tongue_winking_eye"] = "😜",
            ["zany_face"] = "🤪",
            ["stuck_out_tongue_closed_eyes"] = "😝",
            ["money_mouth_face"] = "🤑",
            ["hugs"] = "🤗",
            ["hand_over_mouth"] = "🤭",
            ["shushing_face"] = "🤫",
            ["thinking"] = "🤔",
            ["zipper_mouth_face"] = "🤐",
            ["raised_eyebrow"] = "🤨",
            ["neutral_face"] = "😐",
            ["expressionless"] = "😑",
            ["no_mouth"] = "😶",
            ["smirk"] = "😏",
            ["unamused"] = "😒",
            ["roll_eyes"] = "🙄",
            ["grimacing"] = "😬",
            ["lying_face"] = "🤥",
            ["relieved"] = "😌",
            ["pensive"] = "😔",
            ["sleepy"] = "😪",
            ["drooling_face"] = "🤤",
            ["sleeping"] = "😴",
            ["mask"] = "😷",
            ["face_with_thermometer"] = "🤒",
            ["face_with_head_bandage"] = "🤕",
            ["nauseated_face"] = "🤢",
            ["vomiting_face"] = "🤮",
            ["sneezing_face"] = "🤧",
            ["hot_face"] = "🥵",
            ["cold_face"] = "🥶",
            ["woozy_face"] = "🥴",
            ["dizzy_face"] = "😵",
            ["exploding_head"] = "🤯",
            ["cowboy_hat_face"] = "🤠",
            ["partying_face"] = "🥳",
            ["sunglasses"] = "😎",
            ["nerd_face"] = "🤓",
            ["monocle_face"] = "🧐",
            ["confused"] = "😕",
            ["worried"] = "😟",
            ["slightly_frowning_face"] = "🙁",
            ["open_mouth"] = "😮",
            ["hushed"] = "😯",
            ["astonished"] = "😲",
            ["flushed"] = "😳",
            ["pleading_face"] = "🥺",
            ["frowning"] = "😦",
            ["anguished"] = "😧",
            ["fearful"] = "😨",
            ["cold_sweat"] = "😰",
            ["disappointed_relieved"] = "😥",
            ["cry"] = "😢",
            ["sob"] = "😭",
            ["scream"] = "😱",
            ["confounded"] = "😖",
            ["persevere"] = "😣",
            ["disappointed"] = "😞",
            ["sweat"] = "😓",
            ["weary"] = "😩",
            ["tired_face"] = "😫",
            ["yawning_face"] = "🥱",
            ["triumph"] = "😤",
            ["rage"] = "😡",
            ["angry"] = "😠",
            ["cursing_face"] = "🤬",
            ["smiling_imp"] = "😈",
            ["imp"] = "👿",
            ["skull"] = "💀",
            ["clown_face"] = "🤡",
            ["poop"] = "💩",
            ["ghost"] = "👻",
            ["alien"] = "👽",
            ["robot"] = "🤖",

            // gestures / people
            ["wave"] = "👋",
            ["raised_hand"] = "✋",
            ["ok_hand"] = "👌",
            ["pinching_hand"] = "🤏",
            ["v"] = "✌️",
            ["crossed_fingers"] = "🤞",
            ["love_you_gesture"] = "🤟",
            ["metal"] = "🤘",
            ["call_me_hand"] = "🤙",
            ["point_left"] = "👈",
            ["point_right"] = "👉",
            ["point_up_2"] = "👆",
            ["point_down"] = "👇",
            ["+1"] = "👍",
            ["thumbsup"] = "👍",
            ["-1"] = "👎",
            ["thumbsdown"] = "👎",
            ["fist"] = "✊",
            ["facepunch"] = "👊",
            ["clap"] = "👏",
            ["raised_hands"] = "🙌",
            ["open_hands"] = "👐",
            ["palms_up_together"] = "🤲",
            ["handshake"] = "🤝",
            ["pray"] = "🙏",
            ["muscle"] = "💪",
            ["eyes"] = "👀",
            ["brain"] = "🧠",

            // hearts / symbols
            ["heart"] = "❤️",
            ["orange_heart"] = "🧡",
            ["yellow_heart"] = "💛",
            ["green_heart"] = "💚",
            ["blue_heart"] = "💙",
            ["purple_heart"] = "💜",
            ["black_heart"] = "🖤",
            ["white_heart"] = "🤍",
            ["broken_heart"] = "💔",
            ["two_hearts"] = "💕",
            ["sparkling_heart"] = "💖",
            ["heartpulse"] = "💗",
            ["cupid"] = "💘",
            ["100"] = "💯",
            ["anger"] = "💢",
            ["boom"] = "💥",
            ["dizzy"] = "💫",
            ["sweat_drops"] = "💦",
            ["dash"] = "💨",
            ["fire"] = "🔥",
            ["sparkles"] = "✨",
            ["star"] = "⭐",
            ["star2"] = "🌟",
            ["zap"] = "⚡",
            ["tada"] = "🎉",
            ["confetti_ball"] = "🎊",
            ["balloon"] = "🎈",
            ["gift"] = "🎁",
            ["check"] = "✅",
            ["white_check_mark"] = "✅",
            ["heavy_check_mark"] = "✔️",
            ["x"] = "❌",
            ["negative_squared_cross_mark"] = "❎",
            ["warning"] = "⚠️",
            ["question"] = "❓",
            ["exclamation"] = "❗",
            ["bangbang"] = "‼️",

            // animals
            ["dog"] = "🐶",
            ["cat"] = "🐱",
            ["mouse"] = "🐭",
            ["hamster"] = "🐹",
            ["rabbit"] = "🐰",
            ["fox_face"] = "🦊",
            ["bear"] = "🐻",
            ["panda_face"] = "🐼",
            ["koala"] = "🐨",
            ["tiger"] = "🐯",
            ["lion"] = "🦁",
            ["cow"] = "🐮",
            ["pig"] = "🐷",
            ["frog"] = "🐸",
            ["monkey_face"] = "🐵",
            ["chicken"] = "🐔",
            ["penguin"] = "🐧",
            ["bird"] = "🐦",
            ["unicorn"] = "🦄",
            ["bee"] = "🐝",
            ["bug"] = "🐛",
            ["butterfly"] = "🦋",
            ["snail"] = "🐌",
            ["turtle"] = "🐢",
            ["fish"] = "🐟",
            ["dolphin"] = "🐬",
            ["whale"] = "🐳",
            ["octopus"] = "🐙",

            // food
            ["apple"] = "🍎",
            ["banana"] = "🍌",
            ["watermelon"] = "🍉",
            ["grapes"] = "🍇",
            ["strawberry"] = "🍓",
            ["peach"] = "🍑",
            ["pineapple"] = "🍍",
            ["avocado"] = "🥑",
            ["eggplant"] = "🍆",
            ["hot_pepper"] = "🌶️",
            ["corn"] = "🌽",
            ["bread"] = "🍞",
            ["cheese"] = "🧀",
            ["hamburger"] = "🍔",
            ["fries"] = "🍟",
            ["pizza"] = "🍕",
            ["hotdog"] = "🌭",
            ["taco"] = "🌮",
            ["popcorn"] = "🍿",
            ["doughnut"] = "🍩",
            ["cookie"] = "🍪",
            ["cake"] = "🍰",
            ["birthday"] = "🎂",
            ["chocolate_bar"] = "🍫",
            ["candy"] = "🍬",
            ["coffee"] = "☕",
            ["tea"] = "🍵",
            ["beer"] = "🍺",
            ["beers"] = "🍻",
            ["wine_glass"] = "🍷",
            ["cocktail"] = "🍸",
            ["champagne"] = "🍾",

            // travel / objects / misc
            ["rocket"] = "🚀",
            ["car"] = "🚗",
            ["airplane"] = "✈️",
            ["earth_americas"] = "🌎",
            ["sunny"] = "☀️",
            ["cloud"] = "☁️",
            ["rainbow"] = "🌈",
            ["snowflake"] = "❄️",
            ["snowman"] = "⛄",
            ["ocean"] = "🌊",
            ["moon"] = "🌙",
            ["bulb"] = "💡",
            ["moneybag"] = "💰",
            ["dollar"] = "💵",
            ["gem"] = "💎",
            ["bell"] = "🔔",
            ["lock"] = "🔒",
            ["key"] = "🔑",
            ["hammer"] = "🔨",
            ["wrench"] = "🔧",
            ["gear"] = "⚙️",
            ["computer"] = "💻",
            ["iphone"] = "📱",
            ["camera"] = "📷",
            ["book"] = "📖",
            ["memo"] = "📝",
            ["pencil2"] = "✏️",
            ["clipboard"] = "📋",
            ["calendar"] = "📅",
            ["email"] = "📧",
            ["phone"] = "📞",
            ["mag"] = "🔍",
            ["link"] = "🔗",
            ["hourglass"] = "⌛",
            ["alarm_clock"] = "⏰",
            ["trophy"] = "🏆",
            ["medal"] = "🏅",
            ["dart"] = "🎯",
            ["game_die"] = "🎲",
            ["musical_note"] = "🎵",
            ["notes"] = "🎶",
            ["headphones"] = "🎧",
            ["guitar"] = "🎸",
            ["soccer"] = "⚽",
            ["basketball"] = "🏀",
            ["football"] = "🏈",
            ["baseball"] = "⚾",
            ["tennis"] = "🎾",
            ["8ball"] = "🎱",
            ["thought_balloon"] = "💭",
            ["speech_balloon"] = "💬",
            ["zzz"] = "💤",
        };

    /// <summary>
    /// Returns up to <paramref name="limit"/> entries whose shortcode matches the
    /// given (partial) query. Exact and prefix matches rank ahead of substring
    /// matches; within a rank, shorter shortcodes come first.
    /// </summary>
    public static IReadOnlyList<EmojiEntry> Search(string query, int limit = 8)
    {
        if (string.IsNullOrEmpty(query))
            return Array.Empty<EmojiEntry>();

        query = query.ToLowerInvariant();

        return Map
            .Where(kv => kv.Key.Contains(query, StringComparison.Ordinal))
            .OrderBy(kv => Rank(kv.Key, query))
            .ThenBy(kv => kv.Key.Length)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(limit)
            .Select(kv => new EmojiEntry(kv.Key, kv.Value))
            .ToList();
    }

    /// <summary>Exact glyph for a complete shortcode, or null if unknown.</summary>
    public static string? Exact(string shortcode) =>
        Map.TryGetValue(shortcode.ToLowerInvariant(), out var glyph) ? glyph : null;

    private static int Rank(string key, string query)
    {
        if (key.Equals(query, StringComparison.Ordinal)) return 0;
        if (key.StartsWith(query, StringComparison.Ordinal)) return 1;
        return 2;
    }
}
