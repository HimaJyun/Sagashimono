using CoreTweet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Sagashimono {
    class Program {
        private Tokens tokens;

        static void Main(string[] args) {
            (new Program()).exec(args);
        }

        private void exec(string[] args) {
            var opts = Option(args);
            foreach (var opt in opts) {
                Console.WriteLine($"{opt.Key}: {opt.Value}");
            }
            Console.WriteLine();

            if (opts.Any(value => value.Value == null)) {
                Console.WriteLine("Usage:");
                Console.WriteLine("    -key api_key");
                Console.WriteLine("    -key_secret api_secret");
                Console.WriteLine("    -token token");
                Console.WriteLine("    -token_secret token_secret");
                Console.WriteLine("    -name アカウント");
                Console.WriteLine("    -file 保存先");
                Console.WriteLine("    -count 取得件数");
                return;
            }
            if (!int.TryParse(opts["count"], out var count)) {
                Console.WriteLine($"{opts["count"]} is not Number.");
                return;
            }
            tokens = Tokens.Create(opts["key"], opts["key_secret"], opts["token"], opts["token_secret"]);

            var follow = tokens.Friends.Ids(opts["name"]);
            var followCount = follow.Count;
            var result = new List<Tweet>(followCount * count);
            foreach (var user in follow) {
                try {
                    result.AddRange(GetTweets(user, count));
                } catch (Exception e) {
                    Console.WriteLine($"例外: {e.Message}");
                    if (e.Message == "Rate limit exceeded") {
                        var limit = GetRateLimit();
                        Console.WriteLine($"リクエスト残り枯渇: {limit.Reset.ToString()}まで待機");
                        var time = limit.Reset.ToUnixTimeMilliseconds() - DateTime.Now.Millisecond;
                        if (time > 0) {
                            Thread.Sleep((int)time);
                        }
                    }
                    Thread.Sleep(5000);
                    Console.WriteLine("Retry...");
                    try {
                        result.AddRange(GetTweets(user, count));
                    } catch (Exception e2) {
                        Console.WriteLine($"例外: {e2.Message}");
                        Console.WriteLine("スキップするね");
                    }
                }

                Console.WriteLine($"残り: {--followCount}");
            }
            result.Sort((a, b) => b.Time.CompareTo(a.Time));
            new TsvSerializer<Tweet>(opts["file"]).Serialize(result);
        }

        private IEnumerable<Tweet> GetTweets(long id, int count) {
            Console.WriteLine($"Getting Tweet: {id}");
            int remaining = count;
            long? last = null;

            while (true) {
                if (remaining <= 0) yield break;

                Console.WriteLine($"    Remaining: {remaining},{last}");
                Thread.Sleep(100);
                foreach (var tw in tokens.Statuses.UserTimeline(id, count: remaining, max_id: last)) {
                    remaining--;
                    if (last == tw.Id) { // 前と同じID==取得限界に当たった
                        Console.WriteLine("  遡り限界到達");
                        yield break;
                    }
                    last = tw.Id;
                    yield return new Tweet(tw);
                }
            }
        }

        private RateLimit GetRateLimit() {
            return tokens.Application.RateLimitStatus()["statuses"]["/statuses/user_timeline"];
        }

        private Dictionary<string, string> Option(string[] args) {
            // https://qiita.com/Marimoiro/items/a090344432a5f69e1fac
            args = args.Concat(new string[] { "" }).ToArray();
            var op = new string[] { "-key", "-key_secret", "-token", "-token_secret", "-name", "-file", "-count" };
            return op.ToDictionary(p => p.Substring(1), p => args.SkipWhile(a => a != p).Skip(1).FirstOrDefault());
        }

        public class Tweet {
            public long Id;
            public DateTimeOffset Time;
            public string User;
            public string Text;

            public Tweet(Status status) {
                Id = status.Id;
                Time = status.CreatedAt.ToLocalTime();
                User = status.User.ScreenName;
                Text = status.Text;
            }
            public Tweet() { }
        }
    }
}
