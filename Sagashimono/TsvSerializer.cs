// このソースコードはソフトウェア本体のライセンスに関わらす「zlib license」で自由に利用できます。
// This source code can be used freely with "zlib license" regardless of the license of the software itself.
// http://zlib.net/zlib_license.html
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Sagashimono {
    /// <summary>
    /// 反復可能オブジェクトを簡単にTsvにシリアライズ/デシリアライズ出来るシリアライザー
    /// </summary>
    /// <typeparam name="T">シリアライズするクラス</typeparam>
    public class TsvSerializer<T> where T : new() {
        #region var
        private readonly FileInfo file;
        private readonly Encoding encoding;

        private Dictionary<Type, TypeConverter> converterCache = new Dictionary<Type, TypeConverter>();
        private readonly IEnumerable<KeyValuePair<MemberInfo, string>> memberCache;
        private readonly StringBuilder builder = new StringBuilder(); // 文字変換時の使い回し用
        #endregion

        /// <summary>
        /// BOM無しUTF-8を利用するTsvSerializerクラスのインスタンスを初期化します。
        /// </summary>
        /// <param name="filePath">Tsvのファイルパス</param>
        public TsvSerializer(string filePath) : this(filePath, new UTF8Encoding(false)) { }

        /// <summary>
        /// TsvSerializerクラスのインスタンスを初期化します。
        /// </summary>
        /// <param name="filePath">Tsvのファイルパス</param>
        /// <param name="encoding">ファイルエンコーディング</param>
        public TsvSerializer(string filePath, Encoding encoding) {
            this.file = new FileInfo(filePath);
            this.encoding = encoding;

            AddTypeConverter(typeof(string), new CustomStringConverter());
            memberCache = GetMembers();
        }

        /// <summary>
        /// 反復可能オブジェクトをシリアライズしてTSVに保存します。
        /// </summary>
        /// <param name="items">シリアライズする反復可能オブジェクト</param>
        public void Serialize(IEnumerable<T> items) {
            Serialize(items, false);
        }

        /// <summary>
        /// 反復可能オブジェクトをシリアライズしてTSVに保存します。
        /// </summary>
        /// <param name="items">シリアライズする反復可能オブジェクト</param>
        /// <param name="append">既存のファイルに追記する場合はtrue</param>
        public void Serialize(IEnumerable<T> items, bool append) {
            file.Refresh();
            if (append && (!file.Exists || file.Length == 0)) {
                // 追記モードだがファイルが(存在しない|サイズがゼロ)なら新規モードに切り替える
                append = false;
            }
            FileMode mode = append ? FileMode.Append : FileMode.Create;

            using (var stream = new FileStream(file.FullName, mode, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, encoding)) {
                // 新規モードならヘッダー書き込み
                if (!append) {
                    writer.WriteLine(string.Join("\t", memberCache.Select(x => x.Value)));
                }

                var getters = GetGetters();
                foreach (var item in items) {
                    writer.WriteLine(string.Join("\t",
                        getters.Select(getter => EncodeSpecialCharacter(getter(item)))
                    ));
                }
            }
        }

        /// <summary>
        /// TSVをデシリアライズして復元します。
        /// </summary>
        /// <returns>デシリアライズされた<typeparamref name="T"/>(反復可能)</returns>
        public IEnumerable<T> Deserialize() {
            file.Refresh();
            if (!file.Exists || file.Length == 0) {
                // ファイルがないか空ならなにもしないよ
                yield break;
            }

            using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream, encoding)) {
                // ヘッダ解析
                var setters = GetSetters(reader.ReadLine().Split('\t')).ToArray();

                while (!reader.EndOfStream) {
                    var data = new T();
                    // タブ文字で分解
                    var fields = reader.ReadLine().Split('\t');
                    foreach (var i in Enumerable.Range(0, fields.Length)) {
                        setters[i](data, DecodeSpecialCharacter(fields[i]));
                    }
                    yield return data;
                }
            }
        }

        /// <summary>
        /// 指定した型のTypeConverterを追加します。
        /// </summary>
        /// <param name="type">TypeConverterを利用する型</param>
        /// <param name="converter">追加するTypeConverter、stringとの変換を実装している必要があります</param>
        /// <returns>メソッドチェイン用のthisオブジェクト</returns>
        public TsvSerializer<T> AddTypeConverter(Type type, TypeConverter converter) {
            converterCache[type] = converter;
            return this;
        }

        /// <summary>
        /// このTsvSerializerに紐づくFileInfoを返します。
        /// </summary>
        /// <returns>現在使用中のFileInfo</returns>
        public FileInfo GetFileInfo() {
            return this.file;
        }

        /// <summary>
        /// tab,cr,lf,\を\t,\r,\n,\\に変換します。
        /// </summary>
        /// <param name="value">変換元の文字列</param>
        /// <returns>変換後の文字列</returns>
        private string EncodeSpecialCharacter(string value) {
            return builder
                .Clear()
                .Append(value)
                .Replace(@"\", @"\\")
                .Replace("\t", @"\t").Replace("\r", @"\r").Replace("\n", @"\n")
                .ToString();
        }

        /// <summary>
        /// \t,\r,\n,\\をtab,cr,lf,\に変換します。
        /// </summary>
        /// <param name="value">変換元の文字列</param>
        /// <returns>変換後の文字列</returns>
        private string DecodeSpecialCharacter(string value) {
            return builder
                .Clear()
                .Append(value)
                .Replace(@"\t", "\t").Replace(@"\r", "\r").Replace(@"\n", "\n") // 特殊文字をエスケープ
                .Replace("\\\t", @"\\t").Replace("\\\r", @"\\r").Replace("\\\n", @"\\n") // 誤エスケープを元に戻す
                .Replace(@"\\", @"\") // \\を\に戻す
                .ToString();
        }

        /// <summary>
        /// TypeConverterを取得します。
        /// </summary>
        /// <param name="type">TypeConverterを取得する型</param>
        /// <returns>取得したTypeConverter</returns>
        private TypeConverter GetConverter(Type type) {
            if (!converterCache.ContainsKey(type)) {
                converterCache[type] = TypeDescriptor.GetConverter(type);
            }
            return converterCache[type];
        }

        /// <summary>
        /// Setterを取得します。
        /// </summary>
        /// <param name="header">TSVのヘッダー行</param>
        /// <returns>ConvertFromStringした後にSetValeするデリゲート</returns>
        private IEnumerable<Action<object, string>> GetSetters(string[] header) {
            var members = new Dictionary<string, Action<object, string>>();

            // 型情報取得
            foreach (var type in memberCache) {
                var member = type.Key;
                var name = type.Value;

                if (member.MemberType == MemberTypes.Field) {
                    var info = (FieldInfo)member;
                    var converter = GetConverter(info.FieldType);
                    members[name] = (obj, value) => info.SetValue(obj, converter.ConvertFromString(value));
                } else {
                    var info = (PropertyInfo)member;
                    var converter = GetConverter(info.PropertyType);
                    members[name] = (obj, value) => info.SetValue(obj, converter.ConvertFromString(value));
                }
            }

            // 何番目の値がどの変数に対応するか
            return header.Select((name) => {
                if (!members.ContainsKey(name)) {
                    throw new TsvFormatException("対応していないファイル形式です。" + Environment.NewLine + name + "を解析できませんでした。", file);
                }
                return members[name];
            });
        }

        /// <summary>
        /// Getterを取得します
        /// </summary>
        /// <returns>ConvertToStringした後にGetValueするデリゲート</returns>
        private IEnumerable<Func<object, string>> GetGetters() {
            foreach (var type in memberCache) {
                var member = type.Key;
                var name = type.Value;

                if (member.MemberType == MemberTypes.Field) {
                    var info = (FieldInfo)member;
                    var converter = GetConverter(info.FieldType);
                    yield return (obj => converter.ConvertToString(info.GetValue(obj)));
                } else {
                    var info = (PropertyInfo)member;
                    var converter = GetConverter(info.PropertyType);
                    yield return (obj => converter.ConvertToString(info.GetValue(obj)));
                }
            }
        }

        /// <summary>
        /// <typeparamref name="T"/>を解析します。
        /// </summary>
        /// <returns><typeparamref name="T"/>の取得可能なメンバー</returns>
        private IEnumerable<KeyValuePair<MemberInfo, string>> GetMembers() {
            MemberTypes[] types = { MemberTypes.Property, MemberTypes.Field };
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            foreach (var member in typeof(T).GetMembers(flags).Where(m => types.Contains(m.MemberType))) {
                if (member.MemberType == MemberTypes.Property) {
                    var info = (PropertyInfo)member;
                    // 読み込めない/書き込めないならスキップ
                    if (!info.CanRead || !info.CanWrite) { continue; }
                }

                var attr = member.GetCustomAttributes(typeof(TsvColumnAttribute), true)
                    .Cast<TsvColumnAttribute>()
                    .FirstOrDefault();
                string name = null;

                if (attr != null) {
                    if (attr.Ignore) { continue; }
                    name = attr.Name;
                }
                if (name == null) { name = member.Name; }

                yield return new KeyValuePair<MemberInfo, string>(member, name);
            }
        }

        /// <summary>
        /// カスタムStringコンバーター(標準のStringConverterで発生するnullが空文字になる現象対策)
        /// </summary>
        private class CustomStringConverter : StringConverter {
            /// <summary>
            /// 何らかの値をStringにします、元の値がStringの場合は""を取り除きます。
            /// </summary>
            public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
                var str = value as string;
                if (str == null) {
                    return base.ConvertFrom(context, culture, value);
                }
                if (str == "") { // 空 == null
                    return null;
                }

                // -2 == ""の2文字分
                return str.Substring(1, str.Length - 2);
            }

            /// <summary>
            /// 何らかの値に変換します、変換先の型がStringの場合は""で囲みます。
            /// </summary>
            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType) {
                if (destinationType != typeof(string)) {
                    return base.ConvertTo(context, culture, value, destinationType);
                }
                return value == null ? "" : "\"" + ((string)value) + "\"";
            }
        }
    }

    /// <summary>
    /// TsvSerializerに追加の情報を提供する属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TsvColumnAttribute : Attribute {
        /// <summary>
        /// カラム名、未指定の場合は変数名をそのまま利用
        /// </summary>
        public string Name = null;
        /// <summary>
        /// シリアライズ対象外とする場合はtrue
        /// </summary>
        public bool Ignore = false;
    }

    /// <summary>
    /// Tsvの内容を解析できなかった場合にthrow
    /// </summary>
    public class TsvFormatException : Exception {
        /// <summary>
        /// 例外が発生したファイル、含まれていない場合はnull
        /// </summary>
        public FileInfo File { get; } = null;

        public TsvFormatException() { }
        public TsvFormatException(string message) : base(message) { }
        public TsvFormatException(string message, FileInfo file) : this(message) {
            this.File = file;
        }
        public TsvFormatException(FileInfo file) {
            this.File = file;
        }
    }
}