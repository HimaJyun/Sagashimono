# 幽雅に探せ、過去のツイート
Twitterのフォローしてるユーザーから取り出せるだけツイートを取り出します。  
でもTwitterの制限がキツいので大して取り出せません、正味1週間くらいかな？

「なんか3日前くらいに見かけた気がするツイートが読みたい！」的な時に少しは役に立つのでは？(というかそのために作った訳なんだが)  
そもそも人に使わせるために作ったプログラムじゃないので一切の期待をしてはいけない、単にこのプログラムを自分のストレージで管理するのが面倒だから公開しているだけなの。

## 使い方
- 改良 -> ご自分でどうぞ
- バグ修正 -> ご自分でどうぞ
- その他如何なるサポート -> 一切ありません

以上の項目に納得出来る者のみがここから先に進むことを許される。

用意するもの
1. Twitter APIのKey/KeySecret/Token/TokenSecret
1. Windowsを詠唱基盤とする魔法の箱と、それを世界に繋げられる不思議な力
1. 日本語が読める程度の能力

魔導書に不思議な呪文を唱えながら開きましょう、呪文の一覧は以下の通り。
* -key -> API Key
* -key_secret -> API Key Secret
* -token -> API Token
* -token_secret -> API Token Secret
* -name -> フォローリストの取得元アカウント、
* -file -> 取得したクソを保存する肥溜め
* -count -> 1アカウントにつき何系のツイートを取得するか、デカい数値にしても現実的には1週間分くらいしか取れない

魔導書用法例  
`.\Sagashimono.exe -key "API Key" -key_secret "API Key Secret" -token "API Token" -token_secret "API Token Secret" -name "HimaJyun" -file ".\unko_tweet.tsv" -count 200`

実行時間はフォロー数に比例して増えるので覚悟して唱える事ね。