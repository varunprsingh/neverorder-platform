namespace NeverOrder.Infrastructure.Persistence;

internal static class CatalogSeedData
{
    internal sealed record SeedCategory(string Name, string Slug);

    internal sealed record SeedProduct(
        string Name,
        string Description,
        decimal Price,
        string CategorySlug,
        string ImageSeed,
        int Quantity);

    internal static readonly IReadOnlyList<SeedCategory> Categories = new[]
    {
        new SeedCategory("Electronics", "electronics"),
        new SeedCategory("Food", "food"),
        new SeedCategory("Gaming", "gaming"),
        new SeedCategory("Books", "books"),
        new SeedCategory("Fashion", "fashion"),
        new SeedCategory("Home", "home")
    };

    internal static readonly IReadOnlyList<SeedProduct> Products = new[]
    {
        // Electronics
        new SeedProduct("Aurora Noise-Cancelling Headphones", "Over-ear headphones with adaptive noise cancelling and 40-hour battery life.", 12499m, "electronics", "headphones", 40),
        new SeedProduct("Nimbus Wireless Earbuds", "Compact earbuds with a pocketable charging case and spatial audio.", 4999m, "electronics", "earbuds", 75),
        new SeedProduct("Vertex 27\" 4K Monitor", "A colour-accurate 27-inch display with USB-C power delivery.", 32999m, "electronics", "monitor", 15),
        new SeedProduct("Cobalt Mechanical Keyboard", "Hot-swappable switches, aluminium frame and per-key lighting.", 8999m, "electronics", "keyboard", 30),
        new SeedProduct("Slate Ergonomic Mouse", "A vertical mouse designed to keep your wrist neutral.", 3499m, "electronics", "mouse", 60),
        new SeedProduct("Pulse Portable Charger 20K", "20,000 mAh of backup power with 65W fast charging.", 2999m, "electronics", "powerbank", 90),
        new SeedProduct("Halo Smart Speaker", "Room-filling sound with an assistant that never orders anything.", 6499m, "electronics", "speaker", 25),

        // Food
        new SeedProduct("Single Origin Coffee Beans 1kg", "Medium roast beans with tasting notes of cocoa and orange peel.", 1499m, "food", "coffee", 120),
        new SeedProduct("Artisan Dark Chocolate Box", "Twelve hand-tempered chocolates in a gift box.", 899m, "food", "chocolate", 80),
        new SeedProduct("Cold Pressed Olive Oil 500ml", "First harvest extra virgin olive oil in a light-proof bottle.", 1199m, "food", "oliveoil", 60),
        new SeedProduct("Himalayan Wildflower Honey", "Raw, unfiltered honey collected at high altitude.", 749m, "food", "honey", 100),
        new SeedProduct("Masala Chai Sampler", "Six loose-leaf blends with brewing notes for each.", 999m, "food", "chai", 70),
        new SeedProduct("Sourdough Starter Kit", "Everything needed to keep a starter alive, including a banneton.", 2199m, "food", "sourdough", 35),

        // Gaming
        new SeedProduct("Nova Wireless Controller", "Low-latency controller with hall-effect sticks.", 5999m, "gaming", "controller", 45),
        new SeedProduct("Rift VR Headset", "Standalone headset with inside-out tracking.", 34999m, "gaming", "vr", 10),
        new SeedProduct("Console Charging Dock", "Charges two controllers and shows status at a glance.", 2499m, "gaming", "dock", 55),
        new SeedProduct("Retro Arcade Stick", "Eight-way lever and clicky buttons in a solid wood shell.", 8499m, "gaming", "arcade", 20),
        new SeedProduct("Chronicles of Nowhere (Deluxe)", "An open-world adventure where nothing is ever purchased.", 3999m, "gaming", "gamedeluxe", 65),
        new SeedProduct("Gaming Desk Mat XL", "A 900x400mm stitched-edge mat with a low-friction surface.", 1799m, "gaming", "deskmat", 85),

        // Books
        new SeedProduct("Designing Distributed Systems", "A practical tour of patterns for building reliable services.", 1899m, "books", "bookdistributed", 50),
        new SeedProduct("The Pragmatic Backend", "Hard-won lessons on queues, retries and idempotency.", 1599m, "books", "bookbackend", 45),
        new SeedProduct("Clean Enough Architecture", "When to abstract, and more importantly when not to.", 1399m, "books", "bookarchitecture", 55),
        new SeedProduct("Postgres in Practice", "Indexing, query plans and migrations for application developers.", 2099m, "books", "bookpostgres", 30),
        new SeedProduct("A Short History of Waiting", "Essays on queues, both digital and human.", 899m, "books", "bookwaiting", 70),
        new SeedProduct("The Virtual Economy", "How simulated markets teach real economics.", 1249m, "books", "bookeconomy", 40),

        // Fashion
        new SeedProduct("Heavyweight Cotton Tee", "A 240gsm tee that keeps its shape after every wash.", 1299m, "fashion", "tee", 150),
        new SeedProduct("Merino Crew Sweater", "Fine-gauge merino that layers without bulk.", 4499m, "fashion", "sweater", 45),
        new SeedProduct("Canvas Weekender Bag", "Waxed canvas with leather trim and a wide opening.", 6999m, "fashion", "bag", 25),
        new SeedProduct("Everyday Denim Jacket", "Rigid denim that softens into shape over time.", 5499m, "fashion", "jacket", 35),
        new SeedProduct("Wool Blend Scarf", "A generously sized scarf in a herringbone weave.", 1899m, "fashion", "scarf", 60),
        new SeedProduct("Leather Card Holder", "Slim vegetable-tanned holder for four cards.", 1599m, "fashion", "cardholder", 90),
        new SeedProduct("Trail Running Shoes", "Grippy lugs and a rock plate for uneven ground.", 7999m, "fashion", "shoes", 40),

        // Home
        new SeedProduct("Ceramic Pour-Over Set", "Dripper, carafe and reusable filter in matte ceramic.", 3299m, "home", "pourover", 40),
        new SeedProduct("Linen Bedding Set", "Stonewashed linen that gets softer with every wash.", 8999m, "home", "bedding", 20),
        new SeedProduct("Cast Iron Skillet 12\"", "Pre-seasoned and ready for the stove or the oven.", 2799m, "home", "skillet", 50),
        new SeedProduct("Warm Dimmable Floor Lamp", "A slim arc lamp with a stepless dimmer.", 5999m, "home", "lamp", 30),
        new SeedProduct("Bamboo Desk Organiser", "Keeps cables, pens and a phone off the desk surface.", 1499m, "home", "organiser", 75),
        new SeedProduct("Weighted Throw Blanket", "A 7kg knitted blanket in breathable cotton.", 4299m, "home", "blanket", 28),
        new SeedProduct("Stoneware Mug Set of 4", "Chunky handles and a reactive glaze, no two alike.", 1999m, "home", "mugs", 65)
    };
}
