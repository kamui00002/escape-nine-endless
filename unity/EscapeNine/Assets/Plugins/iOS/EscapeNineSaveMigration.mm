// EscapeNineSaveMigration.mm
// Swift 版セーブデータの一回限り移行 (B-1) のネイティブブリッジ。
// C# 側 (Runtime/SwiftSaveMigration.cs) から P/Invoke で呼ばれ、Swift 正本が書き込んだ
// NSUserDefaults / Keychain の値を読み出して C 文字列で返すだけの薄い読み取り専用ラッパー。
// 変換・正規化ロジックは一切持たない (Core/SwiftSaveConverter.cs の責務)。
//
// 流儀は EscapeNineATT.mm を踏襲: extern "C"、strdup での文字列コピー返却
// (Unity 側が P/Invoke でマーシャリングするためネイティブ側の所有権をそのまま渡す)。
// 本ファイルは移行 1 回限りの読み取り専用処理のため、ATT のような非同期コールバックは不要。

#import <Foundation/Foundation.h>
#import <Security/Security.h>
#import <string.h>

extern "C" {

// NSUserDefaults の文字列配列 (stringArrayForKey:) をカンマ区切り文字列にして返す。
// Swift: UserDefaults.standard.set(unlockedCharacters.map { $0.rawValue }, forKey:) が書き込む形式
// (ネイティブ NSArray<NSString> の plist 配列。JSON ではない)。
// キーが存在しない、または文字列配列型でない場合は NULL を返す。
char *_e9MigStringArrayCsv(const char *key)
{
    if (key == NULL) return NULL;

    NSString *nsKey = [NSString stringWithUTF8String:key];
    NSArray<NSString *> *values = [[NSUserDefaults standardUserDefaults] stringArrayForKey:nsKey];
    if (values == nil) return NULL;

    NSString *csv = [values componentsJoinedByString:@","];
    return strdup([csv UTF8String]);
}

// NSUserDefaults の Data 値 (dataForKey:) を UTF-8 文字列として返す。
// Swift: unlockedAchievements (Codable Set<Achievement> を JSONEncoder で Data 化) が書き込む形式。
// キーが存在しない、Data 型でない、または UTF-8 として解釈できない場合は NULL を返す。
char *_e9MigDataUtf8(const char *key)
{
    if (key == NULL) return NULL;

    NSString *nsKey = [NSString stringWithUTF8String:key];
    NSData *data = [[NSUserDefaults standardUserDefaults] dataForKey:nsKey];
    if (data == nil) return NULL;

    NSString *str = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    if (str == nil) return NULL;

    return strdup([str UTF8String]);
}

// Keychain (kSecClassGenericPassword) から service/account に一致する Data を読み、
// UTF-8 文字列として返す。
// Swift: StoreKitService.keychainWrite (com.escapenine.purchases / purchasedProductIDs、
// JSONEncoder でエンコードした [String] の Data) が書き込む形式。
// 見つからない、または UTF-8 として解釈できない場合は NULL を返す。
char *_e9MigKeychainUtf8(const char *service, const char *account)
{
    if (service == NULL || account == NULL) return NULL;

    NSString *nsService = [NSString stringWithUTF8String:service];
    NSString *nsAccount = [NSString stringWithUTF8String:account];

    NSDictionary *query = @{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: nsService,
        (__bridge id)kSecAttrAccount: nsAccount,
        (__bridge id)kSecReturnData: @YES,
        (__bridge id)kSecMatchLimit: (__bridge id)kSecMatchLimitOne
    };

    CFTypeRef result = NULL;
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
    if (status != errSecSuccess || result == NULL) {
        return NULL;
    }

    NSData *data = (__bridge_transfer NSData *)result;
    NSString *str = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    if (str == nil) return NULL;

    return strdup([str UTF8String]);
}

}
