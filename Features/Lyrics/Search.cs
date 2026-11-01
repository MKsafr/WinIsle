using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

partial class Island
{
    // ---------- LRCLIB ----------
    static string E(string s) { return Uri.EscapeDataString(s); }
    static string Norm(string s) { return Regex.Replace((s ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]+", ""); }

    static string CleanTitle(string t)
    {
        string s = Regex.Replace(t, @"\s*[\(\[【（][^\)\]】）]*[\)\]】）]", " ");
        s = StripVideoSuffix(s);
        s = s.Replace('\u2018', ' ').Replace('\u2019', ' ').Replace('\u201C', ' ').Replace('\u201D', ' ')
            .Replace('\u300C', ' ').Replace('\u300D', ' ').Replace('\u300E', ' ').Replace('\u300F', ' ')
            .Replace('\'', ' ').Replace('"', ' ');
        s = Regex.Replace(s, @"(?i)\s*(feat\.?|ft\.?)\s+.*$", "");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s.Length > 0 ? s : t;
    }

    static string StripVideoSuffix(string title)
    {
        string s = Regex.Replace(title, @"(?i)\s*[-\u2013|]\s*(official|lyrics?|video|audio|topic|remaster).*$", "");
        return Regex.Replace(s, @"(?i)\s*(?:[-\u2013\u2014|]\s*)?(official(?:\s+(?:(?:music|lyric)\s+)?(?:video|mv))?|lyrics?|music\s+video|performance\s+film|video|mv|audio|topic|remaster)\s*$", "");
    }

    static List<string> TitleCandidates(string title, string artist)
    {
        List<string> candidates = new List<string>();
        Match slash = Regex.Match(title, @"[/／]");
        if (slash.Success && Regex.IsMatch(title, @"(?i)\bcover\b|カバー|歌ってみた"))
            candidates.Add(CleanTitle(title.Substring(0, slash.Index)));

        string cleaned = CleanTitle(title);
        if (!string.IsNullOrWhiteSpace(artist))
        {
            string artistPrefix = artist.Trim();
            if (cleaned.Length > artistPrefix.Length &&
                Norm(cleaned.Substring(0, artistPrefix.Length)) == Norm(artistPrefix) &&
                (char.IsWhiteSpace(cleaned[artistPrefix.Length]) || "-\u2013\u2014:|'\u2018\u2019\"\u201C\u201D\u300C\u300E".IndexOf(cleaned[artistPrefix.Length]) >= 0))
            {
                string track = cleaned.Substring(artistPrefix.Length).TrimStart(' ', '-', '\u2013', '\u2014', ':', '|', '\'', '\u2018', '\u2019', '"', '\u201C', '\u201D', '\u300C', '\u300E');
                candidates.Add(CleanTitle(track));
            }
            Match suffix = Regex.Match(cleaned, @"\s*[-\u2013\u2014]\s*(.+)$");
            if (suffix.Success && Norm(suffix.Groups[1].Value) == Norm(artist))
                candidates.Add(CleanTitle(cleaned.Substring(0, suffix.Index)));
        }
        candidates.Add(cleaned);

        List<string> unique = new List<string>();
        foreach (string candidate in candidates)
            if (!string.IsNullOrWhiteSpace(candidate) && !unique.Contains(candidate))
                unique.Add(candidate);
        return unique;
    }

    static string Http(string url, out bool failed) { return Http(url, out failed, "WinIsle/1.0 (https://github.com/MKsafr/VorsWinIsland)", null); }

    static string Http(string url, out bool failed, string ua, string referer)
    {
        failed = false;
        try
        {
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url);
            rq.UserAgent = ua; rq.Timeout = 5000; rq.ReadWriteTimeout = 5000;
            if (referer != null) rq.Referer = referer;
            using (HttpWebResponse rs = (HttpWebResponse)rq.GetResponse())
            using (StreamReader sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }
        catch (WebException ex)
        {
            HttpWebResponse hr = ex.Response as HttpWebResponse;
            if (hr != null && hr.StatusCode == HttpStatusCode.NotFound) return null;
            failed = true; return null;
        }
        catch { failed = true; return null; }
    }

    static LRes ToRes(Dictionary<string, object> d)
    {
        if (d == null) return null;
        LRes r = new LRes();
        r.synced = Str(d, "syncedLyrics"); r.plain = Str(d, "plainLyrics");
        object ins;
        r.instr = d.TryGetValue("instrumental", out ins) && ins is bool && (bool)ins;
        return r;
    }

    static bool Has(LRes r) { return r != null && (r.instr || r.synced.Length > 0 || r.plain.Length > 0); }

    static bool ArtistMatches(string resultArtist, string artist)
    {
        string normalizedArtist = Norm(artist);
        if (normalizedArtist.Length == 0) return true;
        if (Norm(resultArtist) == normalizedArtist) return true;
        foreach (string credit in Regex.Split(resultArtist, @"[,，、&＆/／•·・]|\s+(?:and|feat\.?|ft\.?)\s+", RegexOptions.IgnoreCase))
            if (Norm(credit) == normalizedArtist) return true;
        return false;
    }

    static bool BetterResult(LRes candidate, LRes current)
    {
        if (candidate == null) return false;
        if (current == null) return true;
        if (candidate.score != current.score) return candidate.score > current.score;
        int candidateQuality = candidate.synced.Length > 0 ? 2 : (candidate.plain.Length > 0 ? 1 : 0);
        int currentQuality = current.synced.Length > 0 ? 2 : (current.plain.Length > 0 ? 1 : 0);
        if (candidateQuality != currentQuality) return candidateQuality > currentQuality;
        return false;
    }

    static bool TrackMatches(string name, string track, string artist)
    {
        string normalizedName = Norm(CleanTitle(name)), normalizedTrack = Norm(CleanTitle(track));
        if (normalizedName == normalizedTrack) return true;
        foreach (string candidate in TitleCandidates(name, artist))
            if (Norm(candidate) == Norm(track)) return true;
        foreach (string candidate in TitleCandidates(track, artist))
            if (Norm(candidate) == normalizedName) return true;
        return false;
    }

    static List<string> MatchTokens(string text)
    {
        List<string> tokens = new List<string>();
        foreach (Match match in Regex.Matches((text ?? "").ToLowerInvariant(), @"[\p{L}\p{N}]+"))
        {
            string token = match.Value;
            if (token == "feat" || token == "ft" || token == "featuring" || token == "official" ||
                token == "original" || token == "anime" || token == "music" || token == "lyric" ||
                token == "lyrics" || token == "video" || token == "mv" || token == "audio" ||
                token == "topic" || token == "remaster" || token == "hololive")
                continue;
            if (!tokens.Contains(token)) tokens.Add(token);
        }
        return tokens;
    }

    static void AddUnique(List<string> values, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        foreach (string existing in values)
            if (Norm(existing) == Norm(value)) return;
        values.Add(value);
    }

    static void AddLQuery(List<LQuery> queries, string title, string artist)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        foreach (LQuery q in queries)
            if (Norm(q.title) == Norm(title) && Norm(q.artist) == Norm(artist)) return;
        queries.Add(new LQuery { title = title, artist = artist });
    }

    static LQuery QuotedTrackQuery(string title)
    {
        Match match = Regex.Match(title ?? "", @"^(.*?)['\u2018\u201C\u300C\u300E](.+?)['\u2019\u201D\u300D\u300F](?:\s|$)");
        if (!match.Success) return null;
        string artist = CleanTitle(match.Groups[1].Value);
        string track = CleanTitle(match.Groups[2].Value);
        return artist.Length > 0 && track.Length > 0 ? new LQuery { title = track, artist = artist } : null;
    }

    static List<LQuery> LyricQueries(string title, string artist)
    {
        List<LQuery> queries = new List<LQuery>();
        AddLQuery(queries, title, artist);
        MatchCollection annotations = Regex.Matches(title, @"[\(\[【（]([^\)\]】）]+)[\)\]】）]");
        foreach (Match annotation in annotations)
        {
            string content = annotation.Groups[1].Value;
            Match feature = Regex.Match(content, @"(?i)(?:feat\.?|ft\.?|featuring)\s+(.+)$");
            if (feature.Success)
                foreach (string candidate in TitleCandidates(title, artist))
                    AddLQuery(queries, candidate, feature.Groups[1].Value.Trim());
            if (!Regex.IsMatch(content, @"[/／|&＆、,，]")) continue;
            foreach (string credit in Regex.Split(content, @"[/／|&＆、,，]+"))
                AddLQuery(queries, CleanTitle(title), credit.Trim());
        }
        string symbolTitle = Regex.Replace(title, @"[\(\[【（][^\)\]】）]*[\)\]】）]", " ");
        symbolTitle = StripVideoSuffix(symbolTitle);
        symbolTitle = Regex.Replace(symbolTitle, @"\s*(?:feat\.?|ft\.?)\s+.*$", "", RegexOptions.IgnoreCase);
        List<string> parts = new List<string>();
        foreach (string part in Regex.Split(symbolTitle.Normalize(NormalizationForm.FormKC), @"[\p{P}\p{S}\s]+"))
        {
            string candidate = CleanTitle(part);
            string normalized = Norm(candidate);
            if (normalized.Length < 2 || normalized == "official" || normalized == "music" ||
                normalized == "video" || normalized == "mv" || normalized == "lyrics" ||
                normalized == "lyric" || normalized == "audio" || normalized == "topic" ||
                normalized == "remaster")
                continue;
            if (!parts.Contains(candidate)) parts.Add(candidate);
        }
        for (int i = parts.Count - 1; i > 0; i--)
        {
            AddLQuery(queries, parts[i], parts[i - 1]);
            AddLQuery(queries, parts[i - 1], parts[i]);
        }
        foreach (string candidate in TitleCandidates(title, artist))
            AddLQuery(queries, candidate, artist);
        return queries;
    }

    static double FirstLyricTime(string lyrics)
    {
        foreach (string line in (lyrics ?? "").Replace("\r", "").Split('\n'))
        {
            Match match = Regex.Match(line, @"^\[(\d+):(\d+(?:\.\d+)?)\](.*)$");
            if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[3].Value)) continue;
            double minutes, seconds;
            if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, IC, out minutes) &&
                double.TryParse(match.Groups[2].Value, NumberStyles.Float, IC, out seconds))
                return minutes * 60 + seconds;
        }
        return -1;
    }

    static double ResultScore(string name, string artist, double duration, double requestedDuration, string track, string queryArtist, string preferredTitle, string album, string lyrics)
    {
        List<string> requested = MatchTokens(track);
        List<string> result = MatchTokens(CleanTitle(name));
        int overlap = 0;
        foreach (string token in requested)
            if (result.Contains(token)) overlap++;
        double score = 0;
        if (requested.Count > 0)
        {
            double coverage = overlap / (double)requested.Count;
            double precision = result.Count > 0 ? overlap / (double)result.Count : 0;
            score += coverage * 500 + precision * 200;
            if (Norm(CleanTitle(name)) == Norm(CleanTitle(track))) score += 300;
        }

        if (ArtistMatches(artist, queryArtist)) score += 200;
        MatchCollection features = Regex.Matches(preferredTitle, @"[\(\[【（]\s*(?:feat\.?|ft\.?|featuring)\s+([^\)\]】）]+)[\)\]】）]", RegexOptions.IgnoreCase);
        MatchCollection candidateFeatures = Regex.Matches(name, @"[\(\[【（]\s*(?:feat\.?|ft\.?|featuring)\s+([^\)\]】）]+)[\)\]】）]", RegexOptions.IgnoreCase);
        foreach (Match feature in features)
        {
            string requestedFeature = feature.Groups[1].Value.Trim();
            score += ArtistMatches(artist, requestedFeature) ? 1200 : -1200;
            bool titleNamesFeature = false;
            foreach (Match candidateFeature in candidateFeatures)
                if (ArtistMatches(candidateFeature.Groups[1].Value.Trim(), requestedFeature))
                {
                    titleNamesFeature = true;
                    break;
                }
            score += titleNamesFeature ? 1000 : -1000;
        }

        List<string> versionTokens = new List<string>();
        foreach (Match match in Regex.Matches(preferredTitle, @"(?i)\b(official|video|audio|live|acoustic|remix|edit|demo|instrumental|performance)\b"))
            if (!versionTokens.Contains(match.Value.ToLowerInvariant())) versionTokens.Add(match.Value.ToLowerInvariant());
        string candidateDetails = (name + " " + album).ToLowerInvariant();
        foreach (string token in versionTokens)
            if (candidateDetails.Contains(token)) score += 100;

        bool officialVideo = Regex.IsMatch(preferredTitle, @"(?i)\bofficial\s+(?:(?:music|lyric)\s+)?video\b|\bmusic\s+video\b");
        double firstLyricTime = FirstLyricTime(lyrics);
        if (officialVideo && firstLyricTime >= 0)
        {
            // Official-video uploads can have an intro that is absent from audio-only lyrics.
            if (firstLyricTime >= 15 && firstLyricTime <= 20) score += 600;
            else if (firstLyricTime <= 12) score -= 600;
        }

        if (requestedDuration > 0 && duration > 0)
        {
            double difference = Math.Abs(duration - requestedDuration);
            score += difference < 1 ? 250 : -Math.Min(difference, 45) * 20;
        }
        return score;
    }

    static LRes Pick(object o, double dur, string track, string artist, string preferredTitle)
    {
        System.Collections.IEnumerable arr = o as System.Collections.IEnumerable;
        if (o is Dictionary<string, object> || arr == null) return null;
        LRes best = null;
        foreach (object it in arr)
        {
            Dictionary<string, object> d = it as Dictionary<string, object>;
            if (d == null) continue;
            string name = Str(d, "trackName");
            if (name.Length == 0) name = Str(d, "name");
            if (!TrackMatches(name, track, artist)) continue;
            string resultArtist = Str(d, "artistName");
            if (!ArtistMatches(resultArtist, artist) && !ArtistMatches(name, artist)) continue;
            bool sy = Str(d, "syncedLyrics").Length > 0, pl = Str(d, "plainLyrics").Length > 0;
            object ins; bool instr = d.TryGetValue("instrumental", out ins) && ins is bool && (bool)ins;
            if (!sy && !pl && !instr) continue;
            double dd = 0; double.TryParse(Str(d, "duration"), NumberStyles.Float, IC, out dd);
            if (dur > 0 && dd > 0 && Math.Abs(dd - dur) > 45) continue; // judul dan artis harus persis cocok untuk beda durasi MV
            LRes candidate = ToRes(d);
            candidate.score = ResultScore(name, resultArtist, dd, dur, track, artist, preferredTitle, Str(d, "albumName"), Str(d, "syncedLyrics"));
            if (BetterResult(candidate, best)) best = candidate;
        }
        return best;
    }

    // Urutan: LRCLIB (berwaktu) -> NetEase (berwaktu) -> lirik polos LRCLIB.
    static LRes Fetch(string title, string artist, string album, double dur)
    {
        LRes r = FetchLrclib(title, artist, album, dur);
        if (r != null && !r.err && r.synced.Length > 0) return r;
        LRes ne = null;
        LQuery quotedTrack = QuotedTrackQuery(title);
        if (quotedTrack != null)
            try { ne = FetchNetease(quotedTrack.title, quotedTrack.artist, dur); } catch { }
        List<string> candidates = TitleCandidates(title, artist);
        if ((ne == null || ne.synced.Length == 0) && candidates.Count > 0)
            try { ne = FetchNetease(candidates[0], artist, dur); } catch { }
        if (ne != null && ne.synced.Length > 0) return ne;
        return r;
    }

    // NetEase Cloud Music (API tidak resmi, bisa berubah sewaktu-waktu). Hanya dipakai kalau durasi dan judul/artis cocok.
    static LRes FetchNetease(string title, string artist, double dur)
    {
        const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";
        const string REF = "https://music.163.com/";
        System.Web.Script.Serialization.JavaScriptSerializer ser = new System.Web.Script.Serialization.JavaScriptSerializer();
        ser.MaxJsonLength = int.MaxValue;
        string cleanT = CleanTitle(title);
        if (artist.Length == 0)
        {
            int dash = cleanT.IndexOf(" - ");
            if (dash < 0) dash = cleanT.IndexOf(" \u2013 ");
            if (dash > 0)
            {
                artist = cleanT.Substring(0, dash).Trim();
                cleanT = cleanT.Substring(dash + 3).Trim();
            }
        }
        string nt = Norm(cleanT);
        bool f;
        string js = Http("https://music.163.com/api/search/get/web?csrf_token=&type=1&offset=0&limit=8&s=" + E(cleanT + (artist.Length > 0 ? " " + artist : "")), out f, UA, REF);
        if (js == null) return null;
        Dictionary<string, object> root = ser.DeserializeObject(js) as Dictionary<string, object>;
        if (root == null || !root.ContainsKey("result")) return null;
        Dictionary<string, object> res = root["result"] as Dictionary<string, object>;
        if (res == null || !res.ContainsKey("songs")) return null;
        System.Collections.IEnumerable songs = res["songs"] as System.Collections.IEnumerable;
        if (songs == null) return null;
        string bestId = null; double bestDiff = double.MaxValue;
        foreach (object o in songs)
        {
            Dictionary<string, object> sd = o as Dictionary<string, object>;
            if (sd == null) continue;
            double dd = 0; double.TryParse(Str(sd, "duration"), NumberStyles.Float, IC, out dd); dd /= 1000.0;
            double diff = (dur > 0 && dd > 0) ? Math.Abs(dd - dur) : 999;
            if (dur > 0 && diff > 3) continue;
            string nn = Norm(CleanTitle(Str(sd, "name")));
            bool tOk = nn.Length > 0 && nt.Length > 0 && nn == nt;
            List<string> artistNames = new List<string>();
            System.Collections.IEnumerable ars = sd.ContainsKey("artists") ? sd["artists"] as System.Collections.IEnumerable : null;
            if (ars != null)
                foreach (object ao in ars)
                {
                    Dictionary<string, object> ad = ao as Dictionary<string, object>;
                    string an = ad == null ? "" : Str(ad, "name");
                    if (an.Length > 0) artistNames.Add(an);
                }
            bool aOk = ArtistMatches(string.Join(" • ", artistNames.ToArray()), artist);
            bool ok = tOk && aOk;
            if (ok && diff < bestDiff) { bestDiff = diff; bestId = Str(sd, "id"); }
        }
        if (string.IsNullOrEmpty(bestId)) return null;
        js = Http("https://music.163.com/api/song/lyric?lv=1&kv=1&tv=-1&id=" + E(bestId), out f, UA, REF);
        if (js == null) return null;
        Dictionary<string, object> ly = ser.DeserializeObject(js) as Dictionary<string, object>;
        Dictionary<string, object> lrc = (ly != null && ly.ContainsKey("lrc")) ? ly["lrc"] as Dictionary<string, object> : null;
        string text = lrc == null ? "" : Str(lrc, "lyric");
        if (text.Length == 0) return null;
        StringBuilder sb = new StringBuilder(); int cnt = 0;
        foreach (string line in text.Replace("\r", "").Split('\n'))
        {
            if (!Regex.IsMatch(line, @"^\[\d+:\d+")) continue;
            if (Regex.IsMatch(line, @"^\[[^\]]*\]\s*(\u4F5C\u8BCD|\u4F5C\u66F2|\u7F16\u66F2|\u5236\u4F5C\u4EBA?|\u4F5C\u8A5E|\u7DE8\u66F2|\u8BCD|\u66F2|\u6DF7\u97F3|\u6BCD\u5E26|Lyrics?|Music|Composer|Arranger)\s*[:\uFF1A]", RegexOptions.IgnoreCase)) continue;
            sb.Append(line).Append('\n'); cnt++;
        }
        if (cnt < 5) return null;
        return new LRes { synced = sb.ToString() };
    }

    static LRes FetchLrclib(string title, string artist, string album, double dur)
    {
        System.Web.Script.Serialization.JavaScriptSerializer ser = new System.Web.Script.Serialization.JavaScriptSerializer();
        ser.MaxJsonLength = int.MaxValue;
        const string api = "https://lrclib.net/api/";
        int searchOks = 0, requests = 0; bool f;
        LRes bestResult = null;
        Stopwatch timer = Stopwatch.StartNew();
        List<LQuery> queries = new List<LQuery>();
        LQuery quotedTrack = QuotedTrackQuery(title);
        if (quotedTrack != null) AddLQuery(queries, quotedTrack.title, quotedTrack.artist);
        string rawTrack = CleanTitle(title);
        string artistPrefix = (artist ?? "").Trim();
        MatchCollection features = Regex.Matches(title, @"[\(\[【（]\s*(?:feat\.?|ft\.?|featuring)\s+([^\)\]】）]+)[\)\]】）]", RegexOptions.IgnoreCase);
        string titleArtist = "";
        Match titleArtistMatch = Regex.Match(rawTrack, @"^\s*(.+?)\s+[-\u2013\u2014|]\s+(.+)$");
        if (titleArtistMatch.Success &&
            (artistPrefix.Length == 0 || Norm(artistPrefix).IndexOf(Norm(titleArtistMatch.Groups[1].Value), StringComparison.Ordinal) >= 0 || features.Count > 0))
        {
            titleArtist = titleArtistMatch.Groups[1].Value.Trim();
            rawTrack = titleArtistMatch.Groups[2].Value.Trim();
        }

        List<string> artistCandidates = new List<string>();
        if (features.Count > 0 && titleArtist.Length > 0)
        {
            string featuredTrack = rawTrack + " (feat. " + features[0].Groups[1].Value.Trim() + ")";
            string featuredArtist = titleArtist + " • " + features[0].Groups[1].Value.Trim();
            AddLQuery(queries, featuredTrack, featuredArtist);
            AddLQuery(queries, rawTrack, featuredArtist);
            AddUnique(artistCandidates, featuredArtist);
        }
        AddUnique(artistCandidates, titleArtist);
        AddUnique(artistCandidates, artistPrefix);
        if (features.Count > 0 && titleArtist.Length > 0)
            AddUnique(artistCandidates, titleArtist + " • " + features[0].Groups[1].Value.Trim());
        if (features.Count > 0 && artistPrefix.Length > 0)
            AddUnique(artistCandidates, artistPrefix + " • " + features[0].Groups[1].Value.Trim());
        foreach (Match feature in features) AddUnique(artistCandidates, feature.Groups[1].Value.Trim());
        if (artistCandidates.Count == 0) artistCandidates.Add("");

        if (rawTrack.Length > 0)
            foreach (string queryArtist in artistCandidates)
                AddLQuery(queries, rawTrack, queryArtist);
        foreach (string candidate in TitleCandidates(rawTrack, artist))
            foreach (string queryArtist in artistCandidates)
                AddLQuery(queries, candidate, queryArtist);

        for (int i = 0; i < queries.Count && i < 2; i++)
        {
            if (timer.ElapsedMilliseconds >= 10000) break;
            LQuery query = queries[i];
            if (query.artist.Length == 0) continue;
            string u = api + "get?track_name=" + E(query.title) + "&artist_name=" + E(query.artist);
            if (dur > 0 && dur <= 3600) u += "&duration=" + ((int)Math.Round(dur)).ToString(IC);
            string js = Http(u, out f);
            requests++;
            if (f || js == null) continue;
            try
            {
                Dictionary<string, object> data = ser.DeserializeObject(js) as Dictionary<string, object>;
                string resultTrack = data == null ? "" : Str(data, "trackName");
                if (resultTrack.Length == 0 && data != null) resultTrack = Str(data, "name");
                string resultArtist = data == null ? "" : Str(data, "artistName");
                if (data == null || !TrackMatches(resultTrack, query.title, query.artist) ||
                    (!ArtistMatches(resultArtist, query.artist) && !ArtistMatches(resultTrack, query.artist)))
                    continue;

                LRes r = ToRes(data);
                double resultDuration = 0;
                double.TryParse(Str(data, "duration"), NumberStyles.Float, IC, out resultDuration);
                if (dur > 0 && resultDuration > 0 && Math.Abs(resultDuration - dur) > 45) continue;
                r.score = ResultScore(resultTrack, resultArtist, resultDuration, dur, query.title, query.artist, title, Str(data, "albumName"), r.synced);
                if (Has(r) && BetterResult(r, bestResult)) bestResult = r;
            }
            catch { }
        }
        foreach (LQuery query in queries)
        {
            if (requests >= 5 || timer.ElapsedMilliseconds >= 10000) break;
            string q = query.artist.Length > 0
                ? "track_name=" + E(query.title) + "&artist_name=" + E(query.artist)
                : "q=" + E(query.title);
            string js = Http(api + "search?" + q, out f);
            requests++;
            if (f || js == null) continue;
            try
            {
                object results = ser.DeserializeObject(js);
                searchOks++;
                LRes best = Pick(results, dur, query.title, query.artist, title);
                if (BetterResult(best, bestResult)) bestResult = best;
            }
            catch { }
        }
        if (bestResult != null) return bestResult;
        if (searchOks == 0) return new LRes { err = true };
        return bestResult;
    }

}
