using UnityEngine;
using TMPro;

/// <summary>
/// 직업 상세 패널 안의 프리셋 한 행을 담당합니다.
/// VerticalLayoutGroup 컨테이너 안에 3개 생성됩니다.
/// </summary>
public class PresetRowUI : MonoBehaviour
{
    [Header("UI 참조")]
    public TextMeshProUGUI presetNameText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI mpCostText;
    public TextMeshProUGUI noteText;

    /// <summary>프리셋 데이터로 행을 초기화합니다.</summary>
    public void Setup(JobPreset preset)
    {
        presetNameText.text = preset.presetName;
        attackText.text     = $"공격력  {preset.attackValue}";
        speedText.text      = $"이동속도  {preset.speed}";
        mpCostText.text     = string.IsNullOrEmpty(preset.mpCostValue)
                              ? "MP 소모  없음"
                              : $"MP 소모  {preset.mpCostValue}";

        if (noteText != null)
            noteText.text = string.IsNullOrEmpty(preset.note) ? string.Empty : preset.note;
    }
}
