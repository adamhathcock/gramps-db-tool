using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace GrampsDbTool;

public static class NotNullExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> AsEnumerable<T>(this T item)
    {
        yield return item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> Empty<T>(this IEnumerable<T>? source) => source ?? [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotNull<T>(
        [NotNull] this T? obj,
        [CallerArgumentExpression(nameof(obj))] string? paramName = null
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(obj, paramName);
        return obj;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<T> NotNull<T>(
        this ValueTask<T?> obj,
        [CallerArgumentExpression(nameof(obj))] string? paramName = null
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(obj, paramName);
        return (await obj).NotNull(paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotNull<T>(
        [NotNull] this T? obj,
        [CallerArgumentExpression(nameof(obj))] string? paramName = null
    )
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(obj, paramName);
        return obj.Value;
    }
}