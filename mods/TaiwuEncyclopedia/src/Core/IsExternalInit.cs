// Licensed under MIT. Polyfill for init-only setters on netstandard2.1.
// See: https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-9.0/init
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill for the init-only setter modifier on target frameworks that do not ship this type
    /// (e.g., netstandard2.1). The compiler emits references to this type when it encounters
    /// an `init` accessor; defining it here makes `init` usable on netstandard2.1.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
