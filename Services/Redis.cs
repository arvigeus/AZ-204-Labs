// - Data cache: Cache databases for fast access; syncs with data changes.
// - Content cache: In-memory cache for static web content.
// - Session store: Cache user history for quick retrieval.
// - Job & message queuing: Defer time-consuming tasks for sequential processing.
// - Distributed transactions: Atomic operations via Azure Cache for Redis.
// - Cache-Aside Pattern: On-demand data caching; updates invalidate cache. Use for unpredictable demand, not for web farm session state.

// - Private Caching: Local, fast, not scalable, can be inconsistent, simple, for single-user data.
// - Shared Caching: Common source, slower, scalable, consistent, complex, for multi-user data.

// Eviction Policies:
// - Most-Recently-Used (LIFO)
// - First-In-First-Out
// - Explicit Removal: Based on triggered events like data modification.

// Tiers:
// - Standard
// - Enterprise: redis modules, hosting replica nodes in different availability zones
// - Enterprise Flash: nonvolatile memory, hosting replica nodes in different availability zones

// Session State Providers
// - In Memory: Simple and fast. Not scalable, as it's not distributed.
// - SQL Server: Allows for scalability and persistent storage. Can affect performance, though In-Memory OLTP can improve it.
// - Distributed In Memory (e.g., Azure Cache for Redis): MS self-ad

// TTL (1ms precision): EXPIRE key seconds [NX | XX | GT | LT]

// Key eviction (ex: maxmemory 100mb): allkeys, volatile (has ttl); lru, lfu

// Data persistence
// - RDB: Creates binary snapshots, stored in Azure Storage. Restores cache from latest snapshot.
// - AOF: Logs write operations (negatively affects performance/throughput), saved at least once per second in Azure Storage.

// Supports string and byte[] data

using Newtonsoft.Json;
using StackExchange.Redis;

class RedisService
{
    class MyEntity { }
    async Task<MyEntity> CacheAsidePattern(int id)
    {
        using var redis = ConnectionMultiplexer.Connect("your-redis-connection-string");
        var key = $"MyEntity:{id}";
        var cache = redis.GetDatabase();
        var json = await cache.StringGetAsync(key);
        var value = string.IsNullOrWhiteSpace(json) ? default : JsonConvert.DeserializeObject<MyEntity>(json!);
        if (value == null) // Cache miss
        {
            // value = ...; // Retrieve from data store
            if (value != null)
            {
                await cache.StringSetAsync(key, JsonConvert.SerializeObject(value));
                await cache.KeyExpireAsync(key, TimeSpan.FromMinutes(5));
            }
        }

        RedisResult ping = cache.Execute("ping"); // PONG
        RedisResult clients = await cache.ExecuteAsync("client", "list"); // All the connected clients

        return value!;
    }
}