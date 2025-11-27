#!/usr/bin/env python3
"""
キャラクター画像の背景を透過処理するスクリプト
白または明るい背景を透明にします
"""

from PIL import Image
import os
import glob

def remove_white_background(image_path, output_path=None, threshold=240):
    """
    白背景を透明化する

    Args:
        image_path: 入力画像パス
        output_path: 出力画像パス（Noneの場合は上書き）
        threshold: 白とみなす閾値（0-255）
    """
    if output_path is None:
        output_path = image_path

    # 画像を開く
    img = Image.open(image_path).convert("RGBA")

    # ピクセルデータを取得
    datas = img.getdata()

    new_data = []
    for item in datas:
        # RGB値がすべて閾値以上なら透明化
        if item[0] >= threshold and item[1] >= threshold and item[2] >= threshold:
            # 完全透明
            new_data.append((255, 255, 255, 0))
        else:
            # 既存のアルファ値を維持
            new_data.append(item)

    # 新しいデータを設定
    img.putdata(new_data)

    # 保存
    img.save(output_path, "PNG")
    print(f"✅ 処理完了: {os.path.basename(output_path)}")

def process_assets_folder(assets_path):
    """
    Assets.xcassets内のすべてのキャラクター画像を処理
    """
    # すべてのPNG画像を検索
    png_files = glob.glob(os.path.join(assets_path, "**/*.png"), recursive=True)

    # AppIconは除外
    png_files = [f for f in png_files if "AppIcon" not in f]

    print(f"🔍 {len(png_files)}個の画像を発見しました")
    print("=" * 50)

    for png_file in png_files:
        try:
            remove_white_background(png_file, threshold=240)
        except Exception as e:
            print(f"❌ エラー: {os.path.basename(png_file)} - {e}")

    print("=" * 50)
    print("🎉 すべての処理が完了しました！")

if __name__ == "__main__":
    assets_path = "/Users/yoshidometoru/Documents/GitHub/escape-nine-endless/EscapeNine-endless-/EscapeNine-endless-/Assets.xcassets"

    print("🎨 キャラクター画像の背景透過処理を開始します...")
    print(f"📂 対象フォルダ: {assets_path}")
    print()

    process_assets_folder(assets_path)
