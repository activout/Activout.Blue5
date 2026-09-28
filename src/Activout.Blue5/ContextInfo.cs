namespace Activout.Blue5;

/// <summary>A PAPI context (language or publication), as listed by <see cref="Blue5Client.GetContexts"/>.</summary>
/// <param name="Id">Value to pass to <see cref="Blue5Client.WithContext"/> or <see cref="Blue5Options.Context"/>, e.g. <c>en</c> or <c>l3598</c>.</param>
/// <param name="Name">Display name, e.g. <c>Swedish</c>.</param>
public sealed record ContextInfo(string Id, string? Name);
