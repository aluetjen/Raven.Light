//-----------------------------------------------------------------------
// <copyright file="CompererAndEquality.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System.Collections.Generic;

namespace Raven.Munin
{
    public interface IComparerAndEquality<TKey> : IComparer<TKey>, IEqualityComparer<TKey>
    {
        
    }
}