// Licensed under MIT. Polyfill for init-only setters on netstandard2.1.
// Core/ 有同名 polyfill 但为 internal,Frontend 是独立程序集看不到,需自带一份。
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
