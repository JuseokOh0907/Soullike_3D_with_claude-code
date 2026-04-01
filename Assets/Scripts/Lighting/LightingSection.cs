using UnityEngine;

namespace Project.Lighting
{
    /// <summary>
    /// 씬에 배치하는 섹션 컴포넌트.
    /// Collider(Is Trigger = true)를 함께 부착해야 동작합니다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LightingSection : MonoBehaviour
    {
        [Header("Section Info")]
        public string sectionId = "Section_00";

        [Header("Lighting")]
        public LightingProfile lightingProfile;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning($"[LightingSection] '{sectionId}': Collider was not set as Trigger. Auto-corrected.");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (lightingProfile == null)
            {
                Debug.LogWarning($"[LightingSection] '{sectionId}': LightingProfile is null. Skipping transition.");
                return;
            }

            LightingTransitionManager manager = LightingTransitionManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[LightingSection] LightingTransitionManager instance not found in scene.");
                return;
            }

            manager.TransitionTo(lightingProfile);
        }
    }
}
