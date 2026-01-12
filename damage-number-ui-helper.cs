using UnityEngine;

namespace QuantumMechanic.UI
{
    /// <summary>
    /// Helper component for damage number UI elements.
    /// Tracks spawn time and lifetime for fade-out animations.
    /// </summary>
    public class DamageNumberUI : MonoBehaviour
    {
        /// <summary>
        /// Time when this damage number was spawned.
        /// </summary>
        public float spawnTime;

        /// <summary>
        /// How long this damage number should remain visible.
        /// </summary>
        public float lifetime;

        /// <summary>
        /// Initializes the damage number with timing information.
        /// </summary>
        /// <param name="spawn">The spawn time (usually Time.time).</param>
        /// <param name="life">How long the number should live in seconds.</param>
        public void Initialize(float spawn, float life)
        {
            spawnTime = spawn;
            lifetime = life;
        }
    }
}