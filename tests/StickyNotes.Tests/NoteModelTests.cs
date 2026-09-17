using System.Collections.ObjectModel;
using StickyNotes.Core.Models.Note;
using Xunit;

namespace StickyNotes.Tests;

public class NoteModelTests
{
    [Fact]
    public void NoteModel_Clone_CreatesDeepIndependentCopy()
    {
        var original = new NoteModel
        {
            Id = "orig-123",
            Title = "Master Note",
            Content = "Sensitive text",
            ColorTheme = "yellow",
            Category = "Security",
            IsPinned = true,
            IsAlwaysOnTop = true,
            IsTitleMasked = true,
            IsContentMasked = false,
            Snippets = new ObservableCollection<SnippetBoxModel>
            {
                new() { Type = "CMD", Label = "API Key", Content = "sk_test_12345", IsMasked = true, OrderIndex = 0 },
                new() { Type = "URL", Label = "Endpoint", Content = "https://api.example.com", IsMasked = false, OrderIndex = 1 }
            }
        };

        var clone = original.Clone();

        Assert.NotEqual(original.Id, clone.Id);
        Assert.Equal(original.Title, clone.Title);
        Assert.Equal(original.Content, clone.Content);
        Assert.Equal(original.ColorTheme, clone.ColorTheme);
        Assert.Equal(original.Category, clone.Category);
        Assert.True(clone.IsPinned);
        Assert.True(clone.IsAlwaysOnTop);
        Assert.True(clone.IsTitleMasked);
        Assert.False(clone.IsContentMasked);

        Assert.Equal(2, clone.Snippets.Count);
        Assert.NotEqual(original.Snippets[0].Id, clone.Snippets[0].Id);
        Assert.Equal("API Key", clone.Snippets[0].Label);
        Assert.Equal("sk_test_12345", clone.Snippets[0].Content);
        Assert.True(clone.Snippets[0].IsMasked);

        // Mutate clone and verify original is unaffected
        clone.Title = "Mutated Title";
        clone.Snippets[0].Content = "sk_live_99999";

        Assert.Equal("Master Note", original.Title);
        Assert.Equal("sk_test_12345", original.Snippets[0].Content);
    }

    [Fact]
    public void SnippetBoxModel_MaskingAndDisplayProperties_WorkAsExpected()
    {
        var snippet = new SnippetBoxModel
        {
            Type = "CMD",
            Label = "Password",
            Content = "supersecretpassword",
            IsMasked = true
        };

        Assert.StartsWith("•", snippet.DisplayContent);
        Assert.Equal("\uED1A", snippet.MaskGlyph);
        Assert.Equal("Unhide content", snippet.MaskToolTip);
        Assert.True(snippet.IsSnippet);
        Assert.False(snippet.IsSeparator);

        snippet.IsMasked = false;
        Assert.Equal("supersecretpassword", snippet.DisplayContent);
        Assert.Equal("\uE7B3", snippet.MaskGlyph);
        Assert.Equal("Hide content", snippet.MaskToolTip);
    }

    [Fact]
    public void SnippetBoxModel_TypeClassification_ResolvesCorrectly()
    {
        var separator = new SnippetBoxModel { Type = "SEPARATOR" };
        Assert.True(separator.IsSeparator);
        Assert.False(separator.IsLabel);
        Assert.False(separator.IsDescription);
        Assert.False(separator.IsSnippet);

        var label = new SnippetBoxModel { Type = "LABEL" };
        Assert.True(label.IsLabel);
        Assert.False(label.IsSeparator);
        Assert.False(label.IsSnippet);

        var desc = new SnippetBoxModel { Type = "DESC" };
        Assert.True(desc.IsDescription);
        Assert.False(desc.IsSnippet);
    }

    [Fact]
    public void NoteModel_TitleAndContentDeletion_ReflectsInDisplayTitleAndClone()
    {
        var note = new NoteModel
        {
            Title = "My Title",
            Content = "First line of description",
            HasTitle = true,
            HasContent = true
        };

        Assert.Equal("My Title", note.DisplayTitle);

        // Delete title: should fall back to Content for display
        note.HasTitle = false;
        Assert.Equal("First line of description", note.DisplayTitle);

        // Delete content as well: should fall back to snippet or Untitled Note
        note.HasContent = false;
        note.Snippets.Add(new SnippetBoxModel { Content = "npm run dev" });
        Assert.Equal("npm run dev", note.DisplayTitle);

        // Verify cloning preserves deleted state
        var clone = note.Clone();
        Assert.False(clone.HasTitle);
        Assert.False(clone.HasContent);
    }
}
