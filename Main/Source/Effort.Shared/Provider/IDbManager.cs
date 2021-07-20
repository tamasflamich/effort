// --------------------------------------------------------------------------------------------
// <copyright file="IDbManager.cs" company="Effort Team">
//     Copyright (C) Effort Team
//
//     Permission is hereby granted, free of charge, to any person obtaining a copy
//     of this software and associated documentation files (the "Software"), to deal
//     in the Software without restriction, including without limitation the rights
//     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//     copies of the Software, and to permit persons to whom the Software is
//     furnished to do so, subject to the following conditions:
//
//     The above copyright notice and this permission notice shall be included in
//     all copies or substantial portions of the Software.
//
//     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//     THE SOFTWARE.
// </copyright>
// --------------------------------------------------------------------------------------------

namespace Effort.Provider
{
    /// <summary>
    ///     Provides functionality for managing the database.
    /// </summary>
    public interface IDbManager
    {
        /// <summary>
        ///     Enables or disables all the identity fields in the database.
        /// </summary>
        /// <param name="enabled">
        ///     if set to <c>true</c> the identity fields will be disabled.
        /// </param>
        void SetIdentityFields(bool enabled);

        /// <summary>Set identity information.</summary>
        /// <param name="seed">The identity seed.</param>
        /// <param name="increment">The identity increment.</param>
        void SetIdentity<TEntity>(int? seed, int? increment);

        /// <summary>
        ///     Clears Entity Framework migration history by deleting all records from the 
        ///     appropriate tables.
        /// </summary>
        void ClearMigrationHistory();

        /// <summary>
        ///     Deletes all data from the database tables.
        /// </summary>
        void ClearTables();
    }
}
