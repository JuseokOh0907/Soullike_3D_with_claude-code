using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Lighting
{
    /// <summary>
    /// 섹션 진입 시 LightingProfile을 읽어 조명을 부드럽게 전환합니다.
    /// 씬에 단 하나만 배치하세요.
    /// </summary>
    public class LightingTransitionManager : MonoBehaviour
    {
        public static LightingTransitionManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Light directionalLight;

        // 현재 실행 중인 전환 코루틴
        private Coroutine _activeTransition;

        // 중복 전환 방지용 — 마지막으로 전환을 요청한 프로파일
        private LightingProfile _currentProfile;

        // ─────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[LightingTransitionManager] Duplicate instance found. Destroying this one.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (directionalLight == null)
            {
                directionalLight = FindDirectionalLight();
                if (directionalLight == null)
                    Debug.LogError("[LightingTransitionManager] No Directional Light found. Assign it manually.");
            }
        }

        // ─────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────

        public void TransitionTo(LightingProfile target)
        {
            if (target == null) return;

            // 동일 프로파일 재진입 시 무시
            if (_currentProfile == target) return;

            _currentProfile = target;

            if (_activeTransition != null)
                StopCoroutine(_activeTransition);

            _activeTransition = StartCoroutine(RunTransition(target));
        }

        // ─────────────────────────────────────────
        // Transition Logic
        // ─────────────────────────────────────────

        private IEnumerator RunTransition(LightingProfile target)
        {
            // 스카이박스는 전환 시작 시점에 즉시 교체
            if (target.skyboxMaterial != null)
                RenderSettings.skybox = target.skyboxMaterial;

            // 시작 스냅샷
            LightingSnapshot from = CaptureCurrentState();

            float duration = Mathf.Max(target.transitionDuration, 0.001f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = Mathf.SmoothStep(0f, 1f, t);

                ApplyLerped(from, target, smooth);
                yield return null;
            }

            // 마지막 프레임 보정 — 정확히 목표값으로 고정
            ApplyExact(target);

            _activeTransition = null;
        }

        // ─────────────────────────────────────────
        // Apply Helpers
        // ─────────────────────────────────────────

        private void ApplyLerped(LightingSnapshot from, LightingProfile to, float t)
        {
            if (directionalLight != null)
            {
                directionalLight.color     = Color.Lerp(from.lightColor, to.directionalLightColor, t);
                directionalLight.intensity = Mathf.Lerp(from.lightIntensity, to.directionalLightIntensity, t);
                directionalLight.shadowStrength = Mathf.Lerp(from.shadowStrength, to.shadowStrength, t);
                directionalLight.transform.rotation = Quaternion.Lerp(
                    from.lightRotation,
                    Quaternion.Euler(to.directionalLightEulerAngles),
                    t
                );
            }

            RenderSettings.ambientMode  = to.ambientMode;
            RenderSettings.ambientLight = Color.Lerp(from.ambientColor, to.ambientLightColor, t);

            RenderSettings.fog        = to.fogEnabled;
            RenderSettings.fogColor   = Color.Lerp(from.fogColor, to.fogColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(from.fogDensity, to.fogDensity, t);
        }

        private void ApplyExact(LightingProfile target)
        {
            if (directionalLight != null)
            {
                directionalLight.color          = target.directionalLightColor;
                directionalLight.intensity       = target.directionalLightIntensity;
                directionalLight.shadowStrength  = target.shadowStrength;
                directionalLight.transform.rotation = Quaternion.Euler(target.directionalLightEulerAngles);
            }

            RenderSettings.ambientMode  = target.ambientMode;
            RenderSettings.ambientLight = target.ambientLightColor;

            RenderSettings.fog        = target.fogEnabled;
            RenderSettings.fogColor   = target.fogColor;
            RenderSettings.fogDensity = target.fogDensity;
        }

        // ─────────────────────────────────────────
        // Snapshot & Utility
        // ─────────────────────────────────────────

        private LightingSnapshot CaptureCurrentState()
        {
            var snap = new LightingSnapshot();

            if (directionalLight != null)
            {
                snap.lightColor     = directionalLight.color;
                snap.lightIntensity = directionalLight.intensity;
                snap.shadowStrength = directionalLight.shadowStrength;
                snap.lightRotation  = directionalLight.transform.rotation;
            }
            else
            {
                snap.lightColor     = Color.white;
                snap.lightIntensity = 1f;
                snap.shadowStrength = 1f;
                snap.lightRotation  = Quaternion.identity;
            }

            snap.ambientColor = RenderSettings.ambientLight;
            snap.fogColor     = RenderSettings.fogColor;
            snap.fogDensity   = RenderSettings.fogDensity;

            return snap;
        }

        private static Light FindDirectionalLight()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light l in lights)
            {
                if (l.type == LightType.Directional)
                    return l;
            }
            return null;
        }

        // ─────────────────────────────────────────
        // Snapshot struct
        // ─────────────────────────────────────────

        private struct LightingSnapshot
        {
            public Color      lightColor;
            public float      lightIntensity;
            public float      shadowStrength;
            public Quaternion lightRotation;
            public Color      ambientColor;
            public Color      fogColor;
            public float      fogDensity;
        }
    }
}
