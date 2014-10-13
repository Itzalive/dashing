﻿namespace Dashing.Engine.DapperMapperGeneration {
    using System;

    using Dashing.Engine.DML;

    internal interface IDapperMapperGenerator {
        /// <summary>
        ///     Generates a Func for the passed in fetchTree
        /// </summary>
        /// <typeparam name="T">The base type of the tree</typeparam>
        /// <param name="fetchTree">The fetch tree to generate the mapper for</param>
        /// <returns>A factory for generating mappers</returns>
        Tuple<Delegate, Type[]> GenerateCollectionMapper<T>(FetchNode fetchTree, bool isTracked);

        Tuple<Delegate, Type[]> GenerateNonCollectionMapper<T>(FetchNode fetchTree, bool isTracked);

        Tuple<Delegate, Type[]> GenerateMultiCollectionMapper<T>(FetchNode fetchTree, bool isTracked);
    }
}