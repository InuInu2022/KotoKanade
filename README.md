# KotoKanade (言奏)

<p align="center" style="background-color:lightgreen;padding:2em 0px;">
	<img src="KotoKanade.UI/Assets/appicon/kotokanade.svg" alt="logo" width="200" style="filter: drop-shadow(0 0 3px #000);" />
	<br />
	<strong style="font-family:sans-serif;font-size:2em;color:#03763e;text-shadow:0 0 2px #000;">KotoKanade</strong>
</p>

**KotoKanade** は[VoiSona Talk](https://voisona.com/talk/)を”歌わせる”ツールです。

**KotoKanade**, this is a tool for "Singing" [VoiSona Talk](https://voisona.com/talk/).

---

[![MIT License](http://img.shields.io/badge/license-MIT-blue.svg?style=flat)](LICENSE) [![C Sharp 12](https://img.shields.io/badge/C%20Sharp-12-4FC08D.svg?logo=csharp&style=flat)](https://learn.microsoft.com/ja-jp/dotnet/csharp/) ![.NET 8.0](https://img.shields.io/badge/%20.NET-8.0-blue.svg?logo=dotnet&style=flat)
![GitHub release (latest SemVer including pre-releases)](https://img.shields.io/github/v/release/inuinu2022/KotoKanade?include_prereleases&label=%F0%9F%9A%80release) ![GitHub all releases](https://img.shields.io/github/downloads/InuInu2022/KotoKanade/total?color=green&label=%E2%AC%87%20downloads) ![GitHub Repo stars](https://img.shields.io/github/stars/InuInu2022/KotoKanade?label=%E2%98%85&logo=github)
[![VoiSona Talk](https://img.shields.io/badge/VoiSona_Talk-1.1-53abdb.svg?logo=&style=flat)](https://voisona.com/talk/)

## Video

[![タカハシを歌わせるソフト作ってみた 【KotoKanade】](http://img.youtube.com/vi/UzbBSFkNrrE/mqdefault.jpg)](https://youtu.be/UzbBSFkNrrE?si=YhKhG1W6iTgN99QX)
タカハシを歌わせるソフト作ってみた 【KotoKanade】

## 最新版ダウンロード / Download latest

- **[Download KotoKanade](https://github.com/InuInu2022/KotoKanade/releases/latest)**

- ダウンロード方法
  - 上から一番新しい物を選んでzipファイルをダウンロードして展開して使ってください
  - アップデートはそのまま上書きしてください
  - アンインストールは解凍したフォルダをまるごと消してください

- サポートOS
  - Windows10/11
  - macOS（動作未確認）
  - Linux（動作未確認）

- [ニコニ・コモンズ nc326561](https://commons.nicovideo.jp/works/nc326561)
  - ニコニコ動画等で投稿する作品で使用した場合、利用登録してくださるとうれしいです
  - ※登録は必須ではありません

## Sample

※旧バージョンで制作したものです

- [【VoiSona Talk歌唱】いかないで / 想太 (offset ver.2)【すずきつづみcover】](https://utaloader.net/music/20231229200356536133)
- [【VoiSona Talk歌唱】いかないで / 想太 【タカハシcover】](https://utaloader.net/music/20231229204427612049)
- [「宇宙戦艦"ア"マト / ささきいさお」 タカハシアカペラカバー１フレーズ](https://youtu.be/lnJEOS__mTo)
- [「酔いどれ知らず / Kanaria」 田中傘アカペラカバー](https://youtu.be/LGDpAN4goIs)

## できること

### A. Score Only

歌唱指導を使わずに楽譜データから”歌わせ”ます。
（この段階では）機械的な歌声になります。
ノートに合わせた平坦なピッチを生成します。
※手で調声するベースとしても使えます。

#### 用意するもの

- CeVIOの楽譜ファイル(`.ccs` or `.ccst`)
  - [VoiSona(song)](https://voisona.com/) または [Utaformatix](https://sdercolin.github.io/utaformatix3/)でMIDIなどから変換してください

#### つかいかた

1. ソング用の`ccs` or `ccst`ファイルを用意します
   1. 現在は1トラックのみ対応
2. 読み込みます
3. 歌わせるトークボイスライブラリを選択します
4. 必要ならば細かい設定を行います
5. 「Save」ボタンで出力します
6. 出力された`tstprj`ファイルをVoiSona Talkで読み取り、再生します

#### 制限

- 歌詞は日本語のみ
  - 歌詞はCeVIOソングなどと異なり、漢字かな交じりに対応しています
  - むしろ漢字かな交じりの方が発音が正確になります
- 読み（音素）の差し替えは未対応
  - 逆に助詞の「は」は `[w,a]` と自動で発音します
    - `[h,a]`と喋らせたい場合、歌詞をカタカナの「ハ」にかえるとうまくいきます
- ノートのアーティキュレーション未対応
- ブレス記号はセリフの分割に使われます
  - なのでノートくっつけても大丈夫 むしろ推奨
- 歌詞の記号は無視されます
  - `※`,`$`,`’`,`@`,`%`,`^`,`_`
- 子音オフセット値は固定値
  - 自分で調整する時の基準にしてください
  - 長過ぎる時間を指定すると破綻します！！！
- ノート分割オプションは常にONを推奨
  - 新エンジンのボイス以外は長い発音が途切れるため

#### コツ

- 「は」を`[w,a]`と読んでしまう時
  - 歌詞をカタカナの「ハ」にかえる
- 「きょう」を`[k,y,o]`ではなく`[k,y,u]`とよんでしまう時
  - 歌詞を「きょー」にかえる

### B. Score + Timing

タイミングに歌唱指導を使って歌わせます。
発音はまだ機械的ですが、タイミングが自然になります。
※手で調声するベースとしても使えます。

#### B. 用意するもの

- CeVIOの楽譜ファイル(`.ccs` or `.ccst`)
  - [VoiSona(song)](https://voisona.com/) または [Utaformatix](https://sdercolin.github.io/utaformatix3/)でMIDIなどから変換してください
- 歌唱指導用のタイミング情報ファイル(`.lab`)
  - 上記の楽譜ファイルを歌わせた時に同時生成されるもの
  - CeVIOソング/VoiSona(ソング)などで生成できます

#### 作品投稿の際は…

- 歌唱指導をする際は、ボイスライブラリの利用規約に従ってください
- 歌唱指導した作品を公開するときは、以下のように元のボイスライブラリがわかるようにしてください

> 歌唱指導 さとうささら（CeVIO AI）

#### B. 制限

- 歌詞は日本語のみ
- 読み（音素）の差し替えは**NG**
  - 音素数が異なるとバグります
- 歌詞の記号は無視されます
- ノートのアーティキュレーション未対応
- ノート分割オプションは常にONを推奨
  - 新エンジンのボイス以外は長い発音が途切れるため

### [WIP]C. Score + Timing + Wav

ピッチに歌唱指導を使って歌わせます。
WIP

## DL

[github Release](https://github.com/InuInu2022/KotoKanade/releases/latest)からDL

## Usage

## Build

- .NET SDK 8以降
- [Open JTalk](https://open-jtalk.sourceforge.net/)の辞書が必要です。→[DL先](http://downloads.sourceforge.net/open-jtalk/open_jtalk_dic_utf_8-1.11.tar.gz)
  - `/lib/`フォルダに置いてください

## Licenses

```text
MIT License

Copyright (c) 2024 いぬいぬ
```

<details>

```text
MIT License

Copyright (c) 2024 いぬいぬ

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

</details>

## Resources

- [Epoxy GitHub repository](https://github.com/kekyo/Epoxy)

## Projects

- `KotoKanade.Core`: Independent common component project includes MVVM `Model` code.
- `KotoKanade.UI`: UI (independent platform) project includes MVVM `View` and `ViewModel` code.
- `KotoKanade.Desktop`: The desktop application project code.
- `KotoKanade.Browser`: The web browser application project code.
- `KotoKanade.Android`: Android application project code.
- `KotoKanade.iOS`: iOS application project code.
