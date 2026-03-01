using System.Security.Cryptography;

namespace CandyCoat.Helpers;

public static class ProfileIdHelper
{
    private static readonly string[] Adjectives =
    [
        "silver", "golden", "velvet", "crimson", "azure", "ivory", "misty", "lunar",
        "starlit", "silken", "amber", "opal", "sable", "jade", "violet", "rosy",
        "gilded", "dusky", "pastel", "dewy", "crystal", "coral", "frosty", "peach",
        "shimmery", "glowing", "radiant", "serene", "tender", "dreamy", "wispy",
        "hollow", "gentle", "fleeting", "solemn", "woven", "painted", "faded",
        "blessed", "noble", "sacred", "hidden", "wandering", "soft", "distant",
        "graceful", "timeless", "silent", "eternal", "twilight", "glittered",
        "mellow", "russet", "dappled", "lavender", "emerald", "sapphire", "ruby",
        "obsidian", "celestial", "phantom"
    ];

    private static readonly string[] Nouns =
    [
        "moon", "rose", "petal", "star", "bloom", "veil", "song", "whisper",
        "flame", "dream", "lantern", "shadow", "garden", "breeze", "tide",
        "crystal", "dawn", "dusk", "ember", "feather", "grove", "haven",
        "iris", "jewel", "key", "lily", "mist", "nightfall", "orb", "pearl",
        "quill", "rain", "silk", "thorn", "umber", "vale", "wing", "zephyr",
        "shard", "crown", "path", "echo", "font", "gate", "haze", "isle",
        "lace", "meadow", "nest", "prism", "rune", "scroll", "tale", "urn",
        "vessel", "wish", "yarn", "zenith", "aria", "bell", "candle", "drift"
    ];

    public static string Generate()
    {
        var adj  = Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)];
        var noun = Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)];
        var num  = RandomNumberGenerator.GetInt32(10000);
        return $"{adj}-{noun}-{num:D4}";
    }
}
