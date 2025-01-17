﻿// Copyright (c) Pomelo Foundation. All rights reserved.
// Licensed under the MIT. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Pomelo.EntityFrameworkCore.MySql.Query.ExpressionTranslators.Internal;

/// <summary>
///     An expression that represents a MySQL JSON_TABLE() function call in a SQL tree.
/// </summary>
public class MySqlJsonTableExpression : TableValuedFunctionExpression, IClonableTableExpressionBase
{
    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual SqlExpression JsonExpression
        => Arguments[0];

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual IReadOnlyList<PathSegment> Path { get; }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual IReadOnlyList<ColumnInfo> ColumnInfos { get; }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>

    public MySqlJsonTableExpression(
        string alias,
        SqlExpression jsonExpression,
        IReadOnlyList<PathSegment> path = null,
        IReadOnlyList<ColumnInfo> columnInfos = null)
        : base(alias, "JSON_TABLE", schema: null, builtIn: true, new[] { jsonExpression })
    {
        Path = path;
        ColumnInfos = columnInfos;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var visitedJsonExpression = (SqlExpression)visitor.Visit(JsonExpression);

        PathSegment[] visitedPath = null;

        if (Path is not null)
        {
            for (var i = 0; i < Path.Count; i++)
            {
                var segment = Path[i];
                PathSegment newSegment;

                if (segment.PropertyName is not null)
                {
                    // PropertyName segments are (currently) constants, nothing to visit.
                    newSegment = segment;
                }
                else
                {
                    var newArrayIndex = (SqlExpression)visitor.Visit(segment.ArrayIndex)!;
                    if (newArrayIndex == segment.ArrayIndex)
                    {
                        newSegment = segment;
                    }
                    else
                    {
                        newSegment = new PathSegment(newArrayIndex);

                        if (visitedPath is null)
                        {
                            visitedPath = new PathSegment[Path.Count];
                            for (var j = 0; j < i; i++)
                            {
                                visitedPath[j] = Path[j];
                            }
                        }
                    }
                }

                if (visitedPath is not null)
                {
                    visitedPath[i] = newSegment;
                }
            }
        }

        return Update(visitedJsonExpression, visitedPath ?? Path, ColumnInfos);
    }

    public override TableValuedFunctionExpression Update(IReadOnlyList<SqlExpression> arguments)
        => Update(arguments[0], Path, ColumnInfos);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual MySqlJsonTableExpression Update(
        SqlExpression jsonExpression,
        IReadOnlyList<PathSegment> path,
        IReadOnlyList<ColumnInfo> columnInfos = null)
        => Equals(jsonExpression, JsonExpression)
        && (ReferenceEquals(path, Path) || path is not null && Path is not null && path.SequenceEqual(Path))
        && (ReferenceEquals(columnInfos, ColumnInfos) || columnInfos is not null && ColumnInfos is not null && columnInfos.SequenceEqual(ColumnInfos))
            ? this
            : new MySqlJsonTableExpression(Alias, jsonExpression, path, columnInfos);


    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    // TODO: Deep clone, see #30982
    public virtual TableExpressionBase Clone()
    {
        var clone = new MySqlJsonTableExpression(Alias, JsonExpression, Path, ColumnInfos);

        foreach (var annotation in GetAnnotations())
        {
            clone.AddAnnotation(annotation.Name, annotation.Value);
        }

        return clone;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Append(Name);
        expressionPrinter.Append("(");
        expressionPrinter.Visit(JsonExpression);

        var path = Path ?? Array.Empty<PathSegment>();

        expressionPrinter
            .Append(", '$")
            .Append(string.Join(".", path.Select(e => e.ToString())))
            .Append("'");

        if (ColumnInfos is not null)
        {
            expressionPrinter.Append(" COLUMNS (");

            for (var i = 0; i < ColumnInfos.Count; i++)
            {
                var columnInfo = ColumnInfos[i];

                if (i > 0)
                {
                    expressionPrinter.Append(", ");
                }

                expressionPrinter
                    .Append(columnInfo.Name)
                    .Append(" ")
                    .Append(columnInfo.TypeMapping.StoreType);

                if (columnInfo.Path is not null)
                {
                    expressionPrinter
                        .Append(" PATH '")
                        .Append(string.Join(".", columnInfo.Path.Select(e => e.ToString())))
                        .Append("'");
                }

                if (columnInfo.AsJson)
                {
                    expressionPrinter.Append(" AS JSON");
                }
            }

            expressionPrinter.Append(")");
        }

        expressionPrinter.Append(")");

        PrintAnnotations(expressionPrinter);

        expressionPrinter.Append(" AS ");
        expressionPrinter.Append(Alias);
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
        => ReferenceEquals(this, obj) || (obj is MySqlJsonTableExpression jsonTableExpression && Equals(jsonTableExpression));

    private bool Equals(MySqlJsonTableExpression other)
    {
        if (!base.Equals(other) || ColumnInfos?.Count != other.ColumnInfos?.Count)
        {
            return false;
        }

        if (ReferenceEquals(ColumnInfos, other.ColumnInfos))
        {
            return true;
        }

        for (var i = 0; i < ColumnInfos!.Count; i++)
        {
            var (columnInfo, otherColumnInfo) = (ColumnInfos[i], other.ColumnInfos![i]);

            if (columnInfo.Name != otherColumnInfo.Name
                || !columnInfo.TypeMapping.Equals(otherColumnInfo.TypeMapping)
                || (columnInfo.Path is null != otherColumnInfo.Path is null
                    || (columnInfo.Path is not null
                        && otherColumnInfo.Path is not null
                        && columnInfo.Path.SequenceEqual(otherColumnInfo.Path))))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
        => base.GetHashCode();

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public readonly record struct ColumnInfo(
        string Name,
        RelationalTypeMapping TypeMapping,
        IReadOnlyList<PathSegment> Path = null,
        bool AsJson = false);
}
