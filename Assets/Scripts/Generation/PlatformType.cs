using UnityEngine;

/// <summary>
/// 平台材質類型定義。
/// 日後新增材質時請在此 enum 繼續往下加。
/// </summary>
public enum MaterialType
{
    Concrete,  // 水泥：可站立，鋼索可勾
    Lava,      // 岩漿：鋼索可勾；連線灼燒等見 WardenWinchSystem
    Ice        // 冰：站上去會滑；鋼索可勾、連線減速等見 WardenWinchSystem
    // 日後新增材質在此 enum 繼續往下加
}

/// <summary>
/// 掛在平台根物件上，標記 <see cref="MaterialType"/>。
/// 鋼索系統會檢查此元件；<see cref="type"/> 為水泥／岩漿／冰時可勾住。
/// </summary>
[DisallowMultipleComponent]
public class PlatformType : MonoBehaviour
{
    /// <summary>平台材質類型（鋼索可勾水泥／岩漿／冰）。</summary>
    public MaterialType type = MaterialType.Concrete;

    /// <summary>由生成器或編輯流程設定材質類型。</summary>
    public void SetMaterialType(MaterialType materialType)
    {
        type = materialType;
    }
}
