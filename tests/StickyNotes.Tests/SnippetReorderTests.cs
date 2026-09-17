using StickyNotes.Core.Models.Note;
using Xunit;

namespace StickyNotes.Tests;

public class SnippetReorderTests
{
    [Fact]
    public void NoteSnippets_MoveUpAndDown_ReordersCorrectly()
    {
        var note = new NoteModel();
        var snippet1 = new SnippetBoxModel { Content = "Block 1", OrderIndex = 0 };
        var snippet2 = new SnippetBoxModel { Content = "Block 2", OrderIndex = 1 };
        var snippet3 = new SnippetBoxModel { Content = "Block 3", OrderIndex = 2 };

        note.Snippets.Add(snippet1);
        note.Snippets.Add(snippet2);
        note.Snippets.Add(snippet3);

        // Move snippet2 (index 1) up to index 0
        note.Snippets.Move(1, 0);

        Assert.Equal("Block 2", note.Snippets[0].Content);
        Assert.Equal("Block 1", note.Snippets[1].Content);
        Assert.Equal("Block 3", note.Snippets[2].Content);

        // Move snippet2 (now index 0) down to index 2
        note.Snippets.Move(0, 2);

        Assert.Equal("Block 1", note.Snippets[0].Content);
        Assert.Equal("Block 3", note.Snippets[1].Content);
        Assert.Equal("Block 2", note.Snippets[2].Content);
    }

    [Fact]
    public void SnippetBoxModel_MaskingToggle_HidesAndUnhidesContent()
    {
        var snippet = new SnippetBoxModel { Content = "MySecretPassword123" };
        Assert.False(snippet.IsMasked);
        Assert.Equal("MySecretPassword123", snippet.DisplayContent);

        // Hide / Mask
        snippet.IsMasked = true;
        Assert.True(snippet.IsMasked);
        Assert.Equal(new string('•', 16), snippet.DisplayContent);
        Assert.Equal("\uED1A", snippet.MaskGlyph);

        // Unhide / Reveal
        snippet.IsMasked = false;
        Assert.False(snippet.IsMasked);
        Assert.Equal("MySecretPassword123", snippet.DisplayContent);
        Assert.Equal("\uE7B3", snippet.MaskGlyph);
    }

    [Fact]
    public void NoteModel_TitleAndContent_MaskingAndReordering()
    {
        var note = new NoteModel
        {
            Title = "Confidential Project",
            Content = "Secret roadmap details"
        };

        // Defaults
        Assert.False(note.IsTitleMasked);
        Assert.False(note.IsContentMasked);
        Assert.False(note.IsContentFirst);
        Assert.Equal("Confidential Project", note.DisplayTitle);
        Assert.Equal("Secret roadmap details", note.DisplayContent);

        // Mask title & content
        note.IsTitleMasked = true;
        note.IsContentMasked = true;
        Assert.True(note.IsTitleMasked);
        Assert.True(note.IsContentMasked);
        Assert.StartsWith("•", note.DisplayTitle);
        Assert.StartsWith("•", note.DisplayContent);

        // Reorder (Content before Title)
        note.IsContentFirst = true;
        Assert.True(note.IsContentFirst);

        // Unmask
        note.IsTitleMasked = false;
        note.IsContentMasked = false;
        Assert.Equal("Confidential Project", note.DisplayTitle);
        Assert.Equal("Secret roadmap details", note.DisplayContent);
    }

    [Fact]
    public void TitleAndDescription_CanBeReorderedBetweenAnyElement()
    {
        var note = new NoteModel
        {
            Title = "Sprint Goals",
            Content = "High priority items for this sprint:"
        };

        // Add a snippet and a separator
        note.Snippets.Add(new SnippetBoxModel { Type = "CMD", Label = "Build", Content = "dotnet build", OrderIndex = 0 });
        note.Snippets.Add(new SnippetBoxModel { Type = "SEPARATOR", Content = "Testing", OrderIndex = 1 });

        // Unify blocks so Title and Description become reorderable blocks
        note.EnsureUnifiedBlocks();

        // Should have 4 blocks: Title, Description, CMD snippet, Separator
        Assert.Equal(4, note.Snippets.Count);
        Assert.True(note.Snippets[0].IsTitle);
        Assert.True(note.Snippets[1].IsDescription);
        Assert.True(note.Snippets[2].IsSnippet);
        Assert.True(note.Snippets[3].IsSeparator);

        // Move Title from index 0 down to index 2 (between CMD and Separator)
        note.Snippets.Move(0, 2);
        Assert.True(note.Snippets[0].IsDescription);
        Assert.True(note.Snippets[1].IsSnippet);
        Assert.True(note.Snippets[2].IsTitle);
        Assert.True(note.Snippets[3].IsSeparator);

        // Move Separator from index 3 to the very top (index 0)
        note.Snippets.Move(3, 0);
        Assert.True(note.Snippets[0].IsSeparator);
        Assert.True(note.Snippets[1].IsDescription);
        Assert.True(note.Snippets[2].IsSnippet);
        Assert.True(note.Snippets[3].IsTitle);

        // Move Description from index 1 to the bottom (index 3)
        note.Snippets.Move(1, 3);
        Assert.True(note.Snippets[0].IsSeparator);
        Assert.True(note.Snippets[1].IsSnippet);
        Assert.True(note.Snippets[2].IsTitle);
        Assert.True(note.Snippets[3].IsDescription);

        // Verify DisplayTitle continues to resolve the title block correctly even when at index 2
        Assert.Equal("Sprint Goals", note.DisplayTitle);

        // Verify sync maintains primary properties
        note.SyncFromBlocks();
        Assert.Equal("Sprint Goals", note.Title);
        Assert.Equal("High priority items for this sprint:", note.Content);
    }

    [Fact]
    public void UnifiedBlocks_ReorderSnippetBeforeDescription_PreservesSequenceAndDisplayContent()
    {
        var note = new NoteModel
        {
            Title = "SecretTitle",
            Content = "SecretDescription",
            IsTitleMasked = true,
            IsContentMasked = true
        };

        var snippet1 = new SnippetBoxModel { Type = "CMD", Content = "pass" };
        var snippet2 = new SnippetBoxModel { Type = "CMD", Content = "Hi hello" };

        note.Snippets.Add(snippet1);
        note.Snippets.Add(snippet2);

        note.EnsureUnifiedBlocks();

        // Initial unified order: Title (0), Description (1), pass (2), Hi hello (3)
        Assert.Equal(4, note.Snippets.Count);
        Assert.True(note.Snippets[0].IsTitle);
        Assert.True(note.Snippets[1].IsDescription);
        Assert.Equal("pass", note.Snippets[2].Content);
        Assert.Equal("Hi hello", note.Snippets[3].Content);

        // Reorder: Move 'pass' (index 2) to index 1 (above Description)
        note.Snippets.Move(2, 1);

        // Now order is: Title (0), pass (1), Description (2), Hi hello (3)
        Assert.True(note.Snippets[0].IsTitle);
        Assert.StartsWith("•", note.Snippets[0].DisplayContent);

        Assert.Equal("pass", note.Snippets[1].Content);
        Assert.Equal("pass", note.Snippets[1].DisplayContent);

        Assert.True(note.Snippets[2].IsDescription);
        Assert.StartsWith("•", note.Snippets[2].DisplayContent);

        Assert.Equal("Hi hello", note.Snippets[3].Content);
        Assert.Equal("Hi hello", note.Snippets[3].DisplayContent);
    }
}
