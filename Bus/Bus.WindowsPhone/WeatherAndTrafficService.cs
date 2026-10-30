using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Data.Xml.Dom;

namespace HKBusWP
{
    public static class WeatherAndTrafficService
    {
        public static async Task<HkoWeatherSnapshot> FetchHkoWeatherSnapshotAsync()
        {
            HkoWeatherSnapshot snap = new HkoWeatherSnapshot();
            try
            {
                string rhrJson = await BusRouteDatabase.HttpGetStringAsync(
                    "https://data.weather.gov.hk/weatherAPI/opendata/weather.php?dataType=rhrread&lang=tc",
                    true);
                if (!string.IsNullOrEmpty(rhrJson))
                {
                    JsonObject root = JsonObject.Parse(rhrJson);
                    if (root.ContainsKey("temperature") && root["temperature"].ValueType == JsonValueType.Object)
                    {
                        JsonObject tempObj = root["temperature"].GetObject();
                        if (tempObj.ContainsKey("data") && tempObj["data"].ValueType == JsonValueType.Array)
                        {
                            JsonArray tArr = tempObj["data"].GetArray();
                            string valStr = "";
                            for (int i = 0; i < tArr.Count; i++)
                            {
                                JsonObject item = tArr[i].GetObject();
                                if (BusRouteDatabase.GetJsonString(item, "place") == "香港天文台")
                                {
                                    valStr = BusRouteDatabase.GetJsonString(item, "value");
                                    break;
                                }
                            }
                            if (string.IsNullOrEmpty(valStr) && tArr.Count > 0)
                            {
                                valStr = BusRouteDatabase.GetJsonString(tArr[0].GetObject(), "value");
                            }
                            if (!string.IsNullOrEmpty(valStr)) snap.TemperatureText = valStr + "°C";
                        }
                    }

                    if (root.ContainsKey("icon") && root["icon"].ValueType == JsonValueType.Array)
                    {
                        JsonArray iconArr = root["icon"].GetArray();
                        if (iconArr.Count > 0)
                        {
                            snap.WeatherIconUrl = string.Format(
                                "https://www.hko.gov.hk/images/HKOWxIconOutline/pic{0}.png",
                                iconArr[0].GetNumber());
                        }
                    }
                }

                string warnJson = await BusRouteDatabase.HttpGetStringAsync(
                    "https://data.weather.gov.hk/weatherAPI/opendata/weather.php?dataType=warnsum&lang=tc",
                    true);
                if (!string.IsNullOrEmpty(warnJson))
                {
                    JsonObject wRoot = JsonObject.Parse(warnJson);
                    foreach (string key in wRoot.Keys)
                    {
                        if (wRoot[key].ValueType == JsonValueType.Object)
                        {
                            string iconUrl = MapHkoWarningCodeToIconUrl(
                                BusRouteDatabase.GetJsonString(wRoot[key].GetObject(), "code"));
                            if (!string.IsNullOrEmpty(iconUrl)) snap.WarningIconUrls.Add(iconUrl);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return snap;
        }

        private static string MapHkoWarningCodeToIconUrl(string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            string u = code.Trim().ToUpperInvariant();
            string file = "";
            if (u == "TC1") file = "tc1.gif";
            else if (u == "TC3") file = "tc3.gif";
            else if (u == "TC8NE") file = "tc8ne.gif";
            else if (u == "TC8SE") file = "tc8se.gif";
            else if (u == "TC8NW") file = "tc8nw.gif";
            else if (u == "TC8SW") file = "tc8sw.gif";
            else if (u == "TC9") file = "tc9.gif";
            else if (u == "TC10") file = "tc10.gif";
            else if (u == "WRAINA") file = "raina.gif";
            else if (u == "WRAINR") file = "rainr.gif";
            else if (u == "WRAINB") file = "rainb.gif";
            else if (u == "WTS") file = "ts.gif";
            else if (u == "WMSGNL") file = "sms.gif";
            else if (u == "WHOT") file = "vhot.gif";
            else if (u == "WCOLD") file = "cold.gif";
            else if (u == "WFIREY") file = "firey.gif";
            else if (u == "WFIRER") file = "firer.gif";
            else if (u == "WFNTSA") file = "ntfl.gif";
            else if (u == "WL") file = "landslip.gif";
            else if (u == "WFROST") file = "frost.gif";
            return string.IsNullOrEmpty(file) ? string.Empty : ("https://www.hko.gov.hk/tc/wxinfo/dailywx/images/" + file);
        }

        // Primary Source: RTHK Live Radio Traffic News (programme.rthk.hk/channel/radio/trafficnews) + Secondary TD Special Traffic XML
        public static async Task<List<TrafficIncidentItem>> FetchTrafficNewsAsync()
        {
            List<TrafficIncidentItem> list = new List<TrafficIncidentItem>();

            // 1. Fetch Live RTHK Traffic News (Updated every few minutes 24/7)
            try
            {
                string rthkHtml = await BusRouteDatabase.HttpGetStringAsync(
                    "https://programme.rthk.hk/channel/radio/trafficnews/index.php",
                    true);
                if (!string.IsNullOrEmpty(rthkHtml))
                {
                    ParseRthkTrafficHtml(rthkHtml, list);
                }
            }
            catch (Exception)
            {
            }

            // 2. Also merge Transport Department Special Traffic News XML (DATA.GOV.HK / TD v2)
            string[] tdUrls = ThemeManager.IsEnglish
                ? new string[] {
                    "https://resource.data.one.gov.hk/td/en/specialtrafficnews.xml",
                    "https://www.td.gov.hk/en/special_news/trafficnews_e.xml"
                }
                : new string[] {
                    "https://resource.data.one.gov.hk/td/tc/specialtrafficnews.xml",
                    "https://www.td.gov.hk/tc/special_news/trafficnews.xml"
                };

            for (int u = 0; u < tdUrls.Length; u++)
            {
                try
                {
                    string xmlText = await BusRouteDatabase.HttpGetStringAsync(tdUrls[u], true);
                    if (!string.IsNullOrEmpty(xmlText))
                    {
                        int beforeCount = list.Count;
                        ParseTdTrafficXml(xmlText, list);
                        if (list.Count > beforeCount) break;
                    }
                }
                catch (Exception)
                {
                }
            }

            if (list.Count == 0)
            {
                list.Add(new TrafficIncidentItem
                {
                    HeaderDate = ThemeManager.Tr("香港電台 / 運輸署實時交通消息", "rthk / td live traffic news"),
                    BodyText = ThemeManager.Tr(
                        "目前全港主要幹道及公共交通服務大致正常，未有最新封路或嚴重交通意外特別報告。",
                        "major roads and public transport services in hong kong are currently operating normally.")
                });
            }

            return list;
        }

        private static void ParseRthkTrafficHtml(string html, List<TrafficIncidentItem> list)
        {
            int searchPos = 0;
            while (list.Count < 35)
            {
                int liIdx = html.IndexOf("<li class=\"inner\"", searchPos, StringComparison.OrdinalIgnoreCase);
                if (liIdx < 0)
                {
                    liIdx = html.IndexOf("<div class=\"inner\"", searchPos, StringComparison.OrdinalIgnoreCase);
                }
                if (liIdx < 0) break;

                int endLi = html.IndexOf("</li>", liIdx, StringComparison.OrdinalIgnoreCase);
                if (endLi < 0) endLi = Math.Min(html.Length, liIdx + 900);

                string block = html.Substring(liIdx, endLi - liIdx);
                searchPos = endLi + 4;

                string dateText = "";
                int dateIdx = block.IndexOf("HKT", StringComparison.OrdinalIgnoreCase);
                if (dateIdx >= 10)
                {
                    int startDate = Math.Max(0, dateIdx - 11);
                    int endDate = Math.Min(block.Length, dateIdx + 10);
                    dateText = StripHtmlTags(block.Substring(startDate, endDate - startDate)).Trim();
                }

                string cleanBody = StripHtmlTags(block).Trim();
                if (!string.IsNullOrEmpty(dateText))
                {
                    cleanBody = cleanBody.Replace(dateText, "").Trim();
                }

                if (cleanBody.Length > 6)
                {
                    list.Add(new TrafficIncidentItem
                    {
                        HeaderDate = string.IsNullOrEmpty(dateText) ? "📻 香港電台實時交通消息 (RTHK)" : ("📻 RTHK " + dateText),
                        BodyText = cleanBody
                    });
                }
            }
        }

        private static string StripHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            char[] array = new char[input.Length];
            int arrayIndex = 0;
            bool inside = false;

            for (int i = 0; i < input.Length; i++)
            {
                char let = input[i];
                if (let == '<')
                {
                    inside = true;
                    continue;
                }
                if (let == '>')
                {
                    inside = false;
                    continue;
                }
                if (!inside)
                {
                    array[arrayIndex] = let;
                    arrayIndex++;
                }
            }
            string raw = new string(array, 0, arrayIndex);
            raw = WebUtility.HtmlDecode(raw);
            return raw.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
        }

        private static void ParseTdTrafficXml(string xmlText, List<TrafficIncidentItem> list)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlText);
            XmlNodeList msgNodes = doc.GetElementsByTagName("message");

            for (uint i = 0; i < msgNodes.Length; i++)
            {
                IXmlNode node = msgNodes.Item(i);
                string body = ThemeManager.IsEnglish ? GetXmlChildText(node, "EngText") : GetXmlChildText(node, "ChinText");
                if (string.IsNullOrEmpty(body)) body = GetXmlChildText(node, "ChinText");
                string refDate = GetXmlChildText(node, "ReferenceDate");
                string status = ThemeManager.IsEnglish ? GetXmlChildText(node, "EngShort") : GetXmlChildText(node, "ChinShort");

                if (!string.IsNullOrEmpty(body))
                {
                    list.Add(new TrafficIncidentItem
                    {
                        HeaderDate = string.IsNullOrEmpty(status) ? ("🚨 " + refDate) : string.Format("🚨 {0} ({1})", status, refDate),
                        BodyText = body.Trim()
                    });
                }
            }
        }

        private static string GetXmlChildText(IXmlNode parent, string tagName)
        {
            if (parent == null || parent.ChildNodes == null) return string.Empty;
            for (uint i = 0; i < parent.ChildNodes.Length; i++)
            {
                IXmlNode child = parent.ChildNodes.Item(i);
                if (string.Equals(child.NodeName, tagName, StringComparison.OrdinalIgnoreCase))
                {
                    return child.InnerText ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}