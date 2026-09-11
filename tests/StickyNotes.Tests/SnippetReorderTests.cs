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
}
