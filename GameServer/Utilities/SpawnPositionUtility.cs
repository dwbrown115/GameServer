using System;
using SharedLibrary.Common;

namespace GameServer.Utilities
{
    public static class SpawnPositionUtility
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// Generates a random position within a circle around a center point.
        /// </summary>
        /// <param name="center">The center of the circle (player's position).</param>
        /// <param name="radius">The radius of the circle (spawn radius).</param>
        /// <returns>A new Position object within the specified circle.</returns>
        public static Position GenerateRandomPositionInCircle(Position center, float radius, float noSpawnRadius)
        {
            // Generate a random angle (0 to 2 * PI radians)
            double angle = _random.NextDouble() * 2 * Math.PI;

            // Generate a random distance from the center, ensuring it's between noSpawnRadius and radius
            double distance = noSpawnRadius + (_random.NextDouble() * (radius - noSpawnRadius));

            // Calculate the new X and Y coordinates
            float spawnX = center.X + (float)(distance * Math.Cos(angle));
            float spawnY = center.Y + (float)(distance * Math.Sin(angle));

            return new Position { X = spawnX, Y = spawnY };
        }
    }
}
