using UnityEngine;

/// <summary>
/// 인게임 HUD 좌측에 붙는 제작 탭 버튼 묶음
/// 카테고리별 버튼 클릭 시 CraftPopup을 열고 해당 카테고리를 선택한다
/// </summary>
public class CraftHUDTab : MonoBehaviour
{
    public void OnClickOpenBuilding()
    {
        OpenCategory(CraftCategoryType.Building);
    }

    public void OnClickOpenItem()
    {
        OpenCategory(CraftCategoryType.Item);
    }

    private void OpenCategory(CraftCategoryType _category)
    {
        var existing = Managers.Popup.FindOpen<CraftPopup>();
        if (existing != null)
        {
            existing.SelectCategory(_category);
        }
        else
        {
            var popup = Managers.Popup.Open<CraftPopup>();
            if (popup != null)
                popup.SelectCategory(_category);
        }
    }
}
