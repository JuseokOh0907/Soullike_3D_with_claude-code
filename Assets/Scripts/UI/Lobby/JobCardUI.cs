using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

/// <summary>
/// 캐릭터 선택 창의 직업 카드 하나를 담당합니다.
/// Grid 안에 배치되며, 클릭 시 상위 CharacterSelectUI 에 선택 이벤트를 전달합니다.
/// IPointerClickHandler 를 구현해 카드 전체 영역이 클릭 가능합니다.
/// </summary>
public class JobCardUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI 참조")]
    public TextMeshProUGUI jobNameText;
    public TextMeshProUGUI hpText;
    public Image highlightImage;        // 선택 시 강조 표시용 Image (Color 변경)

    [Header("선택 색상")]
    public Color normalColor   = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    public Color selectedColor = new Color(0.18f, 0.45f, 0.85f, 0.95f);

    private JobData _data;
    private Action<JobData, JobCardUI> _onSelected;

    /// <summary>카드 초기화 — CharacterSelectUI 에서 호출합니다.</summary>
    public void Setup(JobData data, Action<JobData, JobCardUI> onSelected)
    {
        _data = data;
        _onSelected = onSelected;

        jobNameText.text = data.jobNameKR;
        hpText.text = $"HP {data.hp:N0}";

        // 루트 오브젝트에 Graphic Raycaster 가 히트할 수 있도록
        // Image 컴포넌트가 없으면 빈 이미지를 추가합니다.
        if (GetComponent<Graphic>() == null)
        {
            var img = gameObject.AddComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = true;
        }

        SetSelected(false);
    }

    /// <summary>카드 전체 영역 클릭 처리 (IPointerClickHandler)</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        _onSelected?.Invoke(_data, this);
    }

    /// <summary>선택 / 비선택 상태를 시각적으로 전환합니다.</summary>
    public void SetSelected(bool selected)
    {
        if (highlightImage != null)
            highlightImage.color = selected ? selectedColor : normalColor;
    }
}
