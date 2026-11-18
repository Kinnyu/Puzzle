---
name: painting-to-game-scene
description: Adapt an uploaded original painting into a refined 16:9 hand-painted scene for the user's art-exploration game series. Use for new paintings or remakes when the user asks for the series finish or invokes this skill with an image. 將原作改編為統一完成度的精緻手繪遊戲場景。
---

# 藝術探索遊戲場景

使用 [主提示詞](references/master-prompt.md) 將本次提供的原始畫作改編為系列場景。主提示詞可以單獨搭配原作使用。

## 圖像來源

- 每幅新改編與重新改編都以當次指定的原始畫作為主圖。若原作已在當前對話中，沿用該附件或它的可用檔案路徑。使用者明確要求細修某張已生成的圖時，才以該圖作為編輯目標。缺少所需主圖時請使用者提供。
- 預設只使用主圖與主提示詞。使用者要求對照圖，或首稿的清晰度和場景層次需要對照時，可以使用 [《星夜》細修版](assets/starry-night-finish-reference.png) 或使用者指定的參考圖。它提供系列完成度的例子。
- 使用內建 ImageGen。兩張圖皆有本機路徑時，依序傳入主圖與完成度參考圖，在提示詞標註各自用途。若主圖只能以對話附件傳入，優先送入主圖。

## 生成與交付

1. 在主提示詞的基礎上，加入當次原作應保留的觀察，包括主要景物、位置與姿勢、媒材、光線方向、色彩關係和取景方向。直幅通常向兩側延展，超寬畫作通常補足上下。
2. 檢視主要景物與造型是否保有原作辨識度，並確認局部細節、手繪材質、遠近層次以及無文字、UI、邊框、簽名和浮水印。需要重試時，針對具體偏差調整。若使用完成度參考後出現其他畫作的筆觸、色調或造型，移除第二張圖，保留文字標準。
3. 確認 PNG 為精確 16:9 且至少 1920 × 1080；符合條件時保留原生解析度。比例有微小偏差時，先少量裁邊再等比調整。若會裁切到關鍵構圖，調整取景再生成。交付時說明放大或取景改動。
4. 專案圖存入使用者指定或目前專案的交付位置。同名時另存版本。提供檔案連結與尺寸。
