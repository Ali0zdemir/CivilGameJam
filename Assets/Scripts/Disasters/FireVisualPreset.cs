using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CivilGameJam.Disasters
{
    /// <summary>One saved flame/smoke composition, replayed with the same variation sequence.</summary>
    public sealed class FireVisualPreset : ScriptableObject
    {
        [SerializeField] private Mesh[] flameMeshes;
        [SerializeField] private Mesh[] smokeMeshes;
        [SerializeField] private Material flameMaterial;
        [SerializeField] private Material smokeMaterial;
        [SerializeField] private uint sequenceSeed = 7351;
        [SerializeField] private GameObject breathAnimationPrefab;
        [SerializeField] private AnimationClip breathAnimation;
        [SerializeField] private Vector2 breathAnimationRange;

        public uint SequenceSeed => sequenceSeed;
        public GameObject BreathAnimationPrefab => breathAnimationPrefab;
        public AnimationClip BreathAnimation => breathAnimation;
        public Vector2 BreathAnimationRange => breathAnimationRange;

        public int FlameIndex(uint sample) => Variation(sample, flameMeshes.Length);
        public int SmokeIndex(uint sample) => Variation(sample, smokeMeshes.Length);

        private int Variation(uint sample, int count) => (int)(SampleHash(sample) % (uint)count);

        private uint SampleHash(uint sample)
        {
            // Hash the saved sample number, never the time or Unity's global random state.
            var value = sample ^ sequenceSeed;
            value ^= value >> 16; value *= 0x7feb352du;
            value ^= value >> 15; value *= 0x846ca68bu;
            value ^= value >> 16;
            return value;
        }

        public void Configure(ParticleSystem system, bool isSmoke, uint layer)
        {
            var main = system.main;
            main.startRotation3D = true;
            system.useAutoRandomSeed = false;
            system.randomSeed = sequenceSeed + layer;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.SetMeshes(isSmoke ? smokeMeshes : flameMeshes);
            renderer.meshDistribution = ParticleSystemMeshDistribution.UniformRandom;
            renderer.sharedMaterial = isSmoke ? smokeMaterial : flameMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enableGPUInstancing = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color
            });
        }
    }
}
