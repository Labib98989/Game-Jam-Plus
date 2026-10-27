using Unity.Cinemachine;
using UnityEngine;

namespace CoreDump
{
    // Shakes the camera it's on, by however much Juice says: hits, explosions close by, hard
    // landings. It runs on unscaled time, so it keeps shaking through a hit-stop.
    public class CameraShake : CinemachineExtension
    {
        public float maxAngle = 4f;
        public float maxOffset = 0.35f;
        public float frequency = 20f;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise) return;
            float shake = Juice.ShakeAmount;
            if (shake <= 0f) return;
            float t = Time.unscaledTime * frequency;
            state.PositionCorrection += state.RawOrientation * new Vector3(Wobble(t, 0f), Wobble(t, 10f), 0f) * (maxOffset * shake);
            state.OrientationCorrection *= Quaternion.Euler(Wobble(t, 20f) * maxAngle * shake, Wobble(t, 30f) * maxAngle * shake, Wobble(t, 40f) * maxAngle * shake * 0.5f);
        }

        static float Wobble(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;
    }
}
