using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Lighting
{
    [CreateAssetMenu(fileName = "LightingProfile", menuName = "Project/Lighting/Lighting Profile")]
    public class LightingProfile : ScriptableObject
    {
        [Header("Profile Info")]
        public string profileName = "New Profile";

        [Header("Directional Light")]
        public Color directionalLightColor = Color.white;
        [Range(0f, 8f)]
        public float directionalLightIntensity = 1f;
        public Vector3 directionalLightEulerAngles = new Vector3(50f, -30f, 0f);
        [Range(0f, 1f)]
        public float shadowStrength = 1f;

        [Header("Ambient")]
        public AmbientMode ambientMode = AmbientMode.Skybox;
        public Color ambientLightColor = new Color(0.212f, 0.227f, 0.259f);

        [Header("Skybox")]
        public Material skyboxMaterial;

        [Header("Fog")]
        public bool fogEnabled = false;
        public Color fogColor = new Color(0.5f, 0.5f, 0.5f);
        [Range(0f, 1f)]
        public float fogDensity = 0.01f;

        [Header("Transition")]
        [Min(0f)]
        public float transitionDuration = 2f;
    }
}
