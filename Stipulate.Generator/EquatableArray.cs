using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Net.NowhereAtAll.ParameterValidation.Generator;

/// <summary>
/// An immutable array compared by its elements, so that the incremental pipeline can tell an
/// unchanged call site from a changed one and skip regenerating it. Arrays and
/// <see cref="ImmutableArray{T}"/> compare by reference, which would defeat the cache.
/// </summary>
internal readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private const int HashSeed = 17;
    private const int HashMultiplier = 31;

    private ImmutableArray<T> Items { get; } = items;

    public bool Equals(EquatableArray<T> other) => this.Items.SequenceEqual(other.Items);

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && this.Equals(other);

    public override int GetHashCode()
        => this.Items.Aggregate(HashSeed, Combine);

    private static int Combine(int hash, T item) => unchecked((hash * HashMultiplier) + item.GetHashCode());

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)this.Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}
