using Jellyfin.Plugin.AwardsMetadata.TagGeneration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.AwardsMetadata.Tests;

public class ManagedTagStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempFile;
    private readonly JsonManagedTagStore _store;

    public ManagedTagStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "managed-tags-tests-" + Guid.NewGuid().ToString("N"));
        _tempFile = Path.Combine(_tempDir, "managed-tags.json");
        _store = new JsonManagedTagStore(_tempFile, NullLogger<JsonManagedTagStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void GetManagedTags_WhenNoTags_ReturnsEmptySet()
    {
        var tags = _store.GetManagedTags(Guid.NewGuid());
        Assert.Empty(tags);
    }

    [Fact]
    public void SetManagedTags_StoresTags()
    {
        var itemId = Guid.NewGuid();
        var tags = new[] { "tag1", "tag2", "tag3" };

        _store.SetManagedTags(itemId, tags);

        var result = _store.GetManagedTags(itemId);
        Assert.Equal(3, result.Count);
        Assert.Contains("tag1", result);
        Assert.Contains("tag2", result);
        Assert.Contains("tag3", result);
    }

    [Fact]
    public void SetManagedTags_ReplacesExisting()
    {
        var itemId = Guid.NewGuid();
        _store.SetManagedTags(itemId, ["old-tag"]);

        _store.SetManagedTags(itemId, ["new-tag"]);

        var result = _store.GetManagedTags(itemId);
        Assert.Single(result);
        Assert.Contains("new-tag", result);
        Assert.DoesNotContain("old-tag", result);
    }

    [Fact]
    public void RemoveManagedTags_RemovesAllForItem()
    {
        var itemId = Guid.NewGuid();
        _store.SetManagedTags(itemId, ["tag1", "tag2"]);

        _store.RemoveManagedTags(itemId);

        var result = _store.GetManagedTags(itemId);
        Assert.Empty(result);
    }

    [Fact]
    public void GetAllTrackedItemIds_ReturnsAllItems()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _store.SetManagedTags(id1, ["tag1"]);
        _store.SetManagedTags(id2, ["tag2"]);

        var ids = _store.GetAllTrackedItemIds();

        Assert.Equal(2, ids.Count);
        Assert.Contains(id1, ids);
        Assert.Contains(id2, ids);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var itemId = Guid.NewGuid();
        _store.SetManagedTags(itemId, ["tag-a", "tag-b"]);

        await _store.SaveAsync();

        var newStore = new JsonManagedTagStore(_tempFile, NullLogger<JsonManagedTagStore>.Instance);
        await newStore.LoadAsync();

        var tags = newStore.GetManagedTags(itemId);
        Assert.Equal(2, tags.Count);
        Assert.Contains("tag-a", tags);
        Assert.Contains("tag-b", tags);
    }

    [Fact]
    public async Task Load_WhenFileDoesNotExist_InitializesEmpty()
    {
        var nonExistentFile = Path.Combine(_tempDir, "nonexistent.json");
        var store = new JsonManagedTagStore(nonExistentFile, NullLogger<JsonManagedTagStore>.Instance);

        await store.LoadAsync();

        Assert.Empty(store.GetAllTrackedItemIds());
    }

    [Fact]
    public async Task Save_CreatesDirectory()
    {
        Assert.False(Directory.Exists(_tempDir));

        var itemId = Guid.NewGuid();
        _store.SetManagedTags(itemId, ["tag1"]);
        await _store.SaveAsync();

        Assert.True(Directory.Exists(_tempDir));
        Assert.True(File.Exists(_tempFile));
    }

    [Fact]
    public void RemoveManagedTags_DoesNotAffectOtherItems()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _store.SetManagedTags(id1, ["tag1"]);
        _store.SetManagedTags(id2, ["tag2"]);

        _store.RemoveManagedTags(id1);

        Assert.Empty(_store.GetManagedTags(id1));
        Assert.Single(_store.GetManagedTags(id2));
    }
}
