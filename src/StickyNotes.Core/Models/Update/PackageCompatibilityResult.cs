namespace StickyNotes.Core.Models.Update;

/// <summary>
/// Result of verifying the incoming MSIX package compatibility against local system and running package identity.
/// </summary>
public sealed class PackageCompatibilityResult
{
    public bool IsCompatible { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool IdentityMatched { get; init; }
    public bool PublisherMatched { get; init; }
    public bool ArchitectureMatched { get; init; }
    public bool OSVersionSupported { get; init; }

    public static PackageCompatibilityResult Success() => new()
    {
        IsCompatible = true,
        IdentityMatched = true,
        PublisherMatched = true,
        ArchitectureMatched = true,
        OSVersionSupported = true,
        Reason = "Package passes all MSIX identity, architecture, and OS requirements."
    };

    public static PackageCompatibilityResult Failure(string reason, bool identity = false, bool publisher = false, bool arch = false, bool os = false) => new()
    {
        IsCompatible = false,
        Reason = reason,
        IdentityMatched = identity,
        PublisherMatched = publisher,
        ArchitectureMatched = arch,
        OSVersionSupported = os
    };
}
