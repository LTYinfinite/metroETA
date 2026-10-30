using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace HKBusWP
{
    public static class MtrBusService
    {
        private const string MtrBusScheduleUrl = "https://rt.data.gov.hk/v1/transport/mtr/bus/getSchedule";

        // Columns: Code, Dir ("O"/"I"), OrigTc, OrigEn, DestTc, DestEn, IsTwoWay ("1"/"0"), Fare
        private static readonly string[,] _catalogTable = new string[,]
        {
            // East Rail Line Feeder Buses (Tai Po) — Official Fare $4.8
            { "K12", "O", "大埔墟站", "Tai Po Market Station", "大埔八號花園", "Eightland Garden", "1", "$4.8" },
            { "K12", "I", "大埔八號花園", "Eightland Garden", "大埔墟站", "Tai Po Market Station", "1", "$4.8" },
            { "K14", "O", "大埔超級城", "Tai Po Mega Mall", "大埔墟站 (單向)", "Tai Po Market Station", "0", "$4.8" },
            { "K17", "O", "大埔墟站", "Tai Po Market Station", "富善", "Fu Shin", "1", "$4.8" },
            { "K17", "I", "富善", "Fu Shin", "大埔墟站", "Tai Po Market Station", "1", "$4.8" },
            { "K18", "O", "大埔墟站", "Tai Po Market Station", "廣福", "Kwong Fuk", "1", "$4.8" },
            { "K18", "I", "廣福", "Kwong Fuk", "大埔墟站", "Tai Po Market Station", "1", "$4.8" },

            // NWNT MTR Buses (Tuen Mun / Yuen Long / Tin Shui Wai) — Official Fare $5.1
            { "K51", "O", "富泰", "Fu Tai", "大欖", "Tai Lam", "1", "$5.1" },
            { "K51", "I", "大欖", "Tai Lam", "富泰", "Fu Tai", "1", "$5.1" },
            { "K51A", "O", "富泰", "Fu Tai", "掃管笏", "So Kwun Wat", "1", "$5.1" },
            { "K51A", "I", "掃管笏", "So Kwun Wat", "富泰", "Fu Tai", "1", "$5.1" },
            { "K52", "O", "悅湖山莊", "Yuet Wu Villa", "龍鼓灘", "Lung Kwu Tan", "1", "$5.1" },
            { "K52", "I", "龍鼓灘", "Lung Kwu Tan", "悅湖山莊", "Yuet Wu Villa", "1", "$5.1" },
            { "K52A", "O", "屯門站", "Tuen Mun Station", "曾咀", "Tsang Tsui", "1", "$5.1" },
            { "K52A", "I", "曾咀", "Tsang Tsui", "屯門站", "Tuen Mun Station", "1", "$5.1" },
            { "K52P", "O", "龍鼓灘", "Lung Kwu Tan", "屯門站 (單向)", "Tuen Mun Station", "0", "$5.1" },
            { "K53", "O", "屯門站", "Tuen Mun Station", "掃管笏 (循環線)", "So Kwun Wat (Circular)", "0", "$5.1" },
            { "K54", "O", "和田邨", "Wo Tin Estate", "屯門市中心 (循環線)", "Tuen Mun Town Centre (Circular)", "0", "$5.1" },
            { "K58", "O", "富泰", "Fu Tai", "青山灣", "Castle Peak Bay", "1", "$5.1" },
            { "K58", "I", "青山灣", "Castle Peak Bay", "富泰", "Fu Tai", "1", "$5.1" },
            { "K65", "O", "元朗站", "Yuen Long Station", "流浮山", "Lau Fau Shan", "1", "$5.1" },
            { "K65", "I", "流浮山", "Lau Fau Shan", "元朗站", "Yuen Long Station", "1", "$5.1" },
            { "K65A", "O", "天水圍站", "Tin Shui Wai Station", "流浮山", "Lau Fau Shan", "1", "$5.1" },
            { "K65A", "I", "流浮山", "Lau Fau Shan", "天水圍站", "Tin Shui Wai Station", "1", "$5.1" },
            { "K66", "O", "朗屏", "Long Ping", "大棠黃泥墩村", "Tai Tong Wong Nai Tun Tsuen", "1", "$5.1" },
            { "K66", "I", "大棠黃泥墩村", "Tai Tong Wong Nai Tun Tsuen", "朗屏", "Long Ping", "1", "$5.1" },
            { "K68", "O", "元朗工業邨", "Yuen Long Industrial Est", "元朗公園 (循環線)", "Yuen Long Park (Circular)", "0", "$5.1" },
            { "K73", "O", "天恆", "Tin Heng", "元朗西", "Yuen Long West", "1", "$5.1" },
            { "K73", "I", "元朗西", "Yuen Long West", "天恆", "Tin Heng", "1", "$5.1" },
            { "K74", "O", "天水圍市中心", "Tin Shui Wai Town Ctr", "凹頭 (循環線)", "Au Tau (Circular)", "0", "$5.1" },
            { "K75A", "O", "天水圍站", "Tin Shui Wai Station", "洪水橋 (循環線)", "Hung Shui Kiu (Circular)", "0", "$5.1" },
            { "K75P", "O", "天瑞", "Tin Shui", "洪水橋 (循環線)", "Hung Shui Kiu (Circular)", "0", "$5.1" },
            { "K75S", "O", "天水圍站", "Tin Shui Wai Station", "洪福邨 (循環線)", "Hung Fuk Est (Circular)", "0", "$5.1" },
            { "K76", "O", "天恆", "Tin Heng", "天水圍站", "Tin Shui Wai Station", "1", "$5.1" },
            { "K76", "I", "天水圍站", "Tin Shui Wai Station", "天恆", "Tin Heng", "1", "$5.1" },
            { "506", "O", "屯門碼頭", "Tuen Mun Ferry Pier", "兆麟 (循環線)", "Siu Lun (Circular)", "0", "$5.1" }
        };

        // Hardcoded Official MTR Bus Stop Lists: "StopId|NameTc|NameEn|Lat|Lon"
        private static readonly Dictionary<string, string[]> _hardcodedRouteStops = new Dictionary<string, string[]>
        {
            // K12 Outbound: 大埔墟站 -> 八號花園 (Exact 3 stops)
            { "K12_O", new string[] {
                "K12-U010|大埔墟站|Tai Po Market Station|22.444414|114.169471",
                "K12-U020|大埔超級城|Tai Po Mega Mall|22.450971|114.169957",
                "K12-U030|八號花園|Eightland Garden|22.452836|114.166496"
            }},
            // K12 Inbound: 八號花園 -> 大埔墟站 (Exact 4 stops)
            { "K12_I", new string[] {
                "K12-D010|八號花園|Eightland Garden|22.452836|114.166496",
                "K12-D020|大埔超級城|Tai Po Mega Mall|22.451278|114.170087",
                "K12-D030|新達廣場|Uptown Plaza|22.443712|114.169910",
                "K12-D040|大埔墟站|Tai Po Market Station|22.444414|114.169471"
            }},
            // K14 One-Way: 大埔超級城 -> 大埔墟站 (Exact 3 stops, uses -D stop IDs)
            { "K14_O", new string[] {
                "K14-D010|大埔超級城|Tai Po Mega Mall|22.451278|114.170087",
                "K14-D020|新達廣場|Uptown Plaza|22.443712|114.169910",
                "K14-D030|大埔墟站|Tai Po Market Station|22.444414|114.169471"
            }},
            // K17 Outbound: 大埔墟站 -> 富善 (Exact 3 stops)
            { "K17_O", new string[] {
                "K17-U010|大埔墟站|Tai Po Market Station|22.444414|114.169471",
                "K17-U020|大埔中心第五期|Tai Po Centre Phase 5|22.450800|114.170500",
                "K17-U030|富善|Fu Shin|22.454322|114.174098"
            }},
            // K17 Inbound: 富善 -> 大埔墟站 (Exact 3 stops)
            { "K17_I", new string[] {
                "K17-D010|富善|Fu Shin|22.454322|114.174098",
                "K17-D020|新達廣場|Uptown Plaza|22.443712|114.169910",
                "K17-D030|大埔墟站|Tai Po Market Station|22.444414|114.169471"
            }},
            // K18 Outbound: 大埔墟站 -> 廣福 (Exact 2 stops)
            { "K18_O", new string[] {
                "K18-U010|大埔墟站|Tai Po Market Station|22.444414|114.169471",
                "K18-U020|廣福|Kwong Fuk|22.448700|114.174398"
            }},
            // K18 Inbound: 廣福 -> 大埔墟站 (Exact 3 stops)
            { "K18_I", new string[] {
                "K18-D010|廣福|Kwong Fuk|22.448700|114.174398",
                "K18-D020|新達廣場|Uptown Plaza|22.443712|114.169910",
                "K18-D030|大埔墟站|Tai Po Market Station|22.444414|114.169471"
            }},
            // K51 Outbound: 富泰 -> 大欖
            { "K51_O", new string[] {
                "K51-U010|富泰|Fu Tai|22.4075|113.9825",
                "K51-U020|聚康山莊|Beneville|22.4092|113.9802",
                "K51-U030|嶺南大學|Lingnan University|22.4105|113.9815",
                "K51-U040|彩暉花園|Brilliant Garden|22.4112|113.9798",
                "K51-U050|港鐵兆康站 (南)|MTR Siu Hong Station (South)|22.4119|113.9786",
                "K51-U065|景峰花園|Prime View Garden|22.4045|113.9782",
                "K51-U070|雅都商場|Acme Shopping Arcade|22.3985|113.9752",
                "K51-U080|德政圍 (港鐵屯門站)|Tak Ching Court (MTR Tuen Mun Stn)|22.3952|113.9731",
                "K51-U090|屯門市中心|Tuen Mun Town Centre|22.3918|113.9765",
                "K51-U130|三聖邨|Sam Shing Estate|22.3822|113.9788",
                "K51-U170|黃金泳灘|Golden Beach|22.3735|113.9912",
                "K51-U180|香港黃金海岸|Hong Kong Gold Coast|22.3718|113.9935",
                "K51-U220|小欖新村|Siu Lam San Tsuen|22.3695|114.0115",
                "K51-U250|大欖|Tai Lam|22.3685|114.0185"
            }},
            // K51 Inbound: 大欖 -> 富泰
            { "K51_I", new string[] {
                "K51-D010|大欖|Tai Lam|22.3685|114.0185",
                "K51-D030|小欖新村|Siu Lam San Tsuen|22.3695|114.0115",
                "K51-D070|香港黃金海岸|Hong Kong Gold Coast|22.3718|113.9935",
                "K51-D080|黃金泳灘|Golden Beach|22.3735|113.9912",
                "K51-D120|三聖邨|Sam Shing Estate|22.3822|113.9788",
                "K51-D160|屯門市廣場|Tuen Mun Town Plaza|22.3925|113.9762",
                "K51-D180|港鐵屯門站|MTR Tuen Mun Station|22.3952|113.9731",
                "K51-D210|景峰花園|Prime View Garden|22.4045|113.9782",
                "K51-D250|港鐵兆康站 (南)|MTR Siu Hong Station (South)|22.4119|113.9786",
                "K51-D270|嶺南大學|Lingnan University|22.4105|113.9815",
                "K51-D290|富泰|Fu Tai|22.4075|113.9825"
            }},
            // K51A Outbound & Inbound
            { "K51A_O", new string[] {
                "K51A-U010|富泰|Fu Tai|22.4075|113.9825",
                "K51A-U050|港鐵兆康站 (南)|MTR Siu Hong Station (South)|22.4119|113.9786",
                "K51A-U080|港鐵屯門站|MTR Tuen Mun Station|22.3952|113.9731",
                "K51A-U090|屯門市中心|Tuen Mun Town Centre|22.3918|113.9765",
                "K51A-U130|三聖邨|Sam Shing Estate|22.3822|113.9788",
                "K51A-U170|黃金泳灘|Golden Beach|22.3735|113.9912",
                "K51A-U200|星堤|Avignon|22.3742|114.0015",
                "K51A-U220|掃管笏|So Kwun Wat|22.3745|114.0045"
            }},
            { "K51A_I", new string[] {
                "K51A-D010|掃管笏|So Kwun Wat|22.3745|114.0045",
                "K51A-D030|星堤|Avignon|22.3742|114.0015",
                "K51A-D060|黃金泳灘|Golden Beach|22.3735|113.9912",
                "K51A-D100|三聖邨|Sam Shing Estate|22.3822|113.9788",
                "K51A-D140|屯門市廣場|Tuen Mun Town Plaza|22.3925|113.9762",
                "K51A-D160|港鐵屯門站|MTR Tuen Mun Station|22.3952|113.9731",
                "K51A-D210|港鐵兆康站 (南)|MTR Siu Hong Station (South)|22.4119|113.9786",
                "K51A-D250|富泰|Fu Tai|22.4075|113.9825"
            }},
            // K52 Outbound & Inbound
            { "K52_O", new string[] {
                "K52-U010|悅湖山莊|Yuet Wu Villa|22.3725|113.9645",
                "K52-U030|屯門碼頭|Tuen Mun Ferry Pier|22.3721|113.9665",
                "K52-U060|豐景園|Goodview Garden|22.3835|113.9728",
                "K52-U090|屯門市中心|Tuen Mun Town Centre|22.3918|113.9765",
                "K52-U110|港鐵屯門站|MTR Tuen Mun Station|22.3952|113.9731",
                "K52-U140|輕鐵青雲站|LR Tsing Wun Stop|22.3905|113.9675",
                "K52-U180|蝴蝶灣|Butterfly Beach|22.3728|113.9575",
                "K52-U220|望后石|Pillar Point|22.3695|113.9395",
                "K52-U270|龍鼓灘|Lung Kwu Tan|22.3915|113.9195"
            }},
            { "K52_I", new string[] {
                "K52-D010|龍鼓灘|Lung Kwu Tan|22.3915|113.9195",
                "K52-D060|望后石|Pillar Point|22.3695|113.9395",
                "K52-D100|蝴蝶灣|Butterfly Beach|22.3728|113.9575",
                "K52-D140|輕鐵青雲站|LR Tsing Wun Stop|22.3905|113.9675",
                "K52-D170|港鐵屯門站|MTR Tuen Mun Station|22.3952|113.9731",
                "K52-D190|屯門市廣場|Tuen Mun Town Plaza|22.3925|113.9762",
                "K52-D220|豐景園|Goodview Garden|22.3835|113.9728",
                "K52-D250|屯門碼頭|Tuen Mun Ferry Pier|22.3721|113.9665",
                "K52-D270|悅湖山莊|Yuet Wu Villa|22.3725|113.9645"
            }},
            // K52A Outbound & Inbound
            { "K52A_O", new string[] {
                "K52A-U010|港鐵屯門站|MTR Tuen Mun Station|22.3952|113.9731",
                "K52A-U040|輕鐵青雲站|LR Tsing Wun Stop|22.3905|113.9675",
                "K52A-U080|蝴蝶灣|Butterfly Beach|22.3728|113.9575",
                "K52A-U150|龍鼓灘|Lung Kwu Tan|22.3915|113.9195",
                "K52A-U170|曾咀|Tsang Tsui|22.4195|113.9265"
            }},
            { "K52A_I", new string[] {
                "K52A-D010|曾咀|Tsang Tsui|22.4195|113.9265",
                "K52A-D030|龍鼓灘|Lung Kwu Tan|22.3915|113.9195",
                "K52A-D100|蝴蝶灣|Butterfly Beach|22.3728|113.9575",
                "K52A-D150|輕鐵青雲站|LR Tsing Wun Stop|22.3905|113.9675",
                "K52A-D190|港鐵屯門站|MTR Tuen Mun Station|22.3952|113.9731"
            }},
            // K52P One-Way
            { "K52P_O", new string[] {
                "K52P-D010|龍鼓灘|Lung Kwu Tan|22.3915|113.9195",
                "K52P-D100|蝴蝶灣|Butterfly Beach|22.3728|113.9575",
                "K52P-D180|豐景園|Goodview Garden|22.3835|113.9728",
                "K52P-D220|屯門市廣場|Tuen Mun Town Plaza|22.3925|113.9762",
                "K52P-D250|港鐵屯門站|MTR Tuen Mun Station|22.3952|113.9731"
            }},
            // K53 Circular
            { "K53_O", new string[] {
                "K53-U010|屯門站 (輕鐵屯門站)|Tuen Mun Station (LR Tuen Mun Stop)|22.394225|113.973358",
                "K53-U030|屯門市中心|Tuen Mun Town Centre|22.3918|113.9765",
                "K53-U070|三聖邨|Sam Shing Estate|22.3822|113.9788",
                "K53-U110|黃金泳灘|Golden Beach|22.3735|113.9912",
                "K53-U150|星堤|Avignon|22.3742|114.0015",
                "K53-U170|掃管笏村|So Kwun Wat Tsuen|22.3745|114.0045",
                "K53-U220|香港黃金海岸|Hong Kong Gold Coast|22.3718|113.9935",
                "K53-U280|屯門市廣場|Tuen Mun Town Plaza|22.3925|113.9762",
                "K53-U300|屯門站|Tuen Mun Station|22.394225|113.973358"
            }},
            // K54 Circular
            { "K54_O", new string[] {
                "K54-U010|和田邨|Wo Tin Estate|22.412954|113.968639",
                "K54-U020|菁田邨|Ching Tin Estate|22.4112|113.9715",
                "K54-U030|欣田邨|Yan Tin Estate|22.4125|113.9755",
                "K54-U040|港鐵兆康站 (北)|MTR Siu Hong Station (North)|22.4122|113.9788",
                "K54-U050|紅橋|Hung Kiu|22.4015|113.9772",
                "K54-U060|屯門市廣場|Tuen Mun Town Plaza|22.3925|113.9762",
                "K54-U070|港鐵屯門站|MTR Tuen Mun Station|22.3952|113.9731",
                "K54-U090|港鐵兆康站 (北)|MTR Siu Hong Station (North)|22.4122|113.9788",
                "K54-U120|和田邨|Wo Tin Estate|22.412954|113.968639"
            }},
            // K58 Outbound & Inbound
            { "K58_O", new string[] {
                "K58-U010|富泰|Fu Tai|22.4075|113.9825",
                "K58-U050|港鐵兆康站 (南)|MTR Siu Hong Station (South)|22.4119|113.9786",
                "K58-U090|良景邨|Leung King Estate|22.4068|113.9638",
                "K58-U110|建生邨|Kin Sang Estate|22.4052|113.9695",
                "K58-U150|港鐵屯門站|MTR Tuen Mun Station|22.3952|113.9731",
                "K58-U170|屯門市中心|Tuen Mun Town Centre|22.3918|113.9765",
                "K58-U220|青山灣|Castle Peak Bay|22.3815|113.9795"
            }},
            { "K58_I", new string[] {
                "K58-D010|青山灣|Castle Peak Bay|22.3815|113.9795",
                "K58-D050|屯門市廣場|Tuen Mun Town Plaza|22.3925|113.9762",
                "K58-D070|港鐵屯門站|MTR Tuen Mun Station|22.3952|113.9731",
                "K58-D110|建生邨|Kin Sang Estate|22.4052|113.9695",
                "K58-D130|良景邨|Leung King Estate|22.4068|113.9638",
                "K58-D180|港鐵兆康站 (南)|MTR Siu Hong Station (South)|22.4119|113.9786",
                "K58-D220|富泰|Fu Tai|22.4075|113.9825"
            }},
            // K65 Outbound & Inbound
            { "K65_O", new string[] {
                "K65-U010|元朗站 (港鐵元朗站)|Yuen Long Station (MTR Yuen Long Stn)|22.445144|114.035187",
                "K65-U030|元朗大棠路|Yuen Long Tai Tong Road|22.4445|114.0295",
                "K65-U050|水邊圍站|Shui Pin Wai Stop|22.4448|114.0202",
                "K65-U080|屏山|Ping Shan|22.4438|114.0105",
                "K65-U100|港鐵天水圍站|MTR Tin Shui Wai Station|22.446556|114.003731",
                "K65-U110|天盛苑|Tin Shing Court|22.4458|114.0015",
                "K65-U150|廈村|Ha Tsuen|22.4515|113.9925",
                "K65-U210|流浮山|Lau Fau Shan|22.4685|113.9845"
            }},
            { "K65_I", new string[] {
                "K65-D010|流浮山|Lau Fau Shan|22.4685|113.9845",
                "K65-D070|廈村|Ha Tsuen|22.4515|113.9925",
                "K65-D110|天盛苑|Tin Shing Court|22.4458|114.0015",
                "K65-D120|港鐵天水圍站|MTR Tin Shui Wai Station|22.446556|114.003731",
                "K65-D140|屏山|Ping Shan|22.4438|114.0105",
                "K65-D170|元朗康樂路|Yuen Long Hong Lok Road|22.4445|114.0265",
                "K65-D190|元朗站|Yuen Long Station|22.445144|114.035187"
            }},
            // K65A Outbound & Inbound
            { "K65A_O", new string[] {
                "K65A-U010|天水圍站|Tin Shui Wai Station|22.446556|114.003731",
                "K65A-U020|天盛苑|Tin Shing Court|22.4458|114.0015",
                "K65A-U060|廈村|Ha Tsuen|22.4515|113.9925",
                "K65A-U120|流浮山|Lau Fau Shan|22.4685|113.9845"
            }},
            { "K65A_I", new string[] {
                "K65A-D010|流浮山|Lau Fau Shan|22.4685|113.9845",
                "K65A-D070|廈村|Ha Tsuen|22.4515|113.9925",
                "K65A-D110|天盛苑|Tin Shing Court|22.4458|114.0015",
                "K65A-D120|天水圍站|Tin Shui Wai Station|22.446556|114.003731"
            }},
            // K66 Outbound & Inbound
            { "K66_O", new string[] {
                "K66-U010|朗屏|Long Ping|22.4478|114.0252",
                "K66-U030|元朗千色匯|Kolour Yuen Long|22.4428|114.0285",
                "K66-U070|蝶翠峰|Sereno Verde|22.4335|114.0278",
                "K66-U120|大棠山道|Tai Tong Shan Road|22.4215|114.0282",
                "K66-U140|大棠黃泥墩村|Tai Tong Wong Nai Tun Tsuen|22.4165|114.0285"
            }},
            { "K66_I", new string[] {
                "K66-D010|大棠黃泥墩村|Tai Tong Wong Nai Tun Tsuen|22.4165|114.0285",
                "K66-D030|大棠山道|Tai Tong Shan Road|22.4215|114.0282",
                "K66-D080|蝶翠峰|Sereno Verde|22.4335|114.0278",
                "K66-D120|元朗教育路|Yuen Long Kau Yuk Road|22.4432|114.0268",
                "K66-D150|朗屏|Long Ping|22.4478|114.0252"
            }},
            // K68 Circular
            { "K68_O", new string[] {
                "K68-U010|元朗工業邨|Yuen Long Industrial Estate|22.460827|114.026513",
                "K68-U050|朗屏邨|Long Ping Estate|22.4495|114.0238",
                "K68-U070|元朗安寧路|Yuen Long On Ning Road|22.4462|114.0258",
                "K68-U100|元朗公園|Yuen Long Park|22.4415|114.0185",
                "K68-U130|元朗體育路|Yuen Long Tai Yuk Road|22.4438|114.0225",
                "K68-U150|港鐵朗屏站|MTR Long Ping Station|22.4478|114.0252",
                "K68-U190|元朗工業邨|Yuen Long Industrial Estate|22.460827|114.026513"
            }},
            // K73 Outbound & Inbound
            { "K73_O", new string[] {
                "K73-U010|天恆|Tin Heng|22.469006|113.999661",
                "K73-U020|天逸邨逸潭樓|Yat Tam House Tin Yat Estate|22.468098|114.0005",
                "K73-U040|天晴邨晴彩樓|Ching Choi House Tin Ching Est|22.4638|114.0032",
                "K73-U060|嘉湖山莊美湖居|Maywood Court Kingswood Villas|22.4605|114.0052",
                "K73-U090|天慈邨|Tin Tsz Estate|22.4535|114.0068",
                "K73-U100|朗屏邨|Long Ping Estate|22.4495|114.0238",
                "K73-U120|元朗西|Yuen Long West|22.4445|114.0245"
            }},
            { "K73_I", new string[] {
                "K73-D010|元朗西|Yuen Long West|22.4445|114.0245",
                "K73-D030|朗屏邨|Long Ping Estate|22.4495|114.0238",
                "K73-D050|天水圍公園|Tin Shui Wai Park|22.4558|114.0025",
                "K73-D070|天瑞邨|Tin Shui Estate|22.4565|113.9985",
                "K73-D090|天頌苑|Tin Chung Court|22.4598|113.9982",
                "K73-D110|天富苑|Tin Fu Court|22.4635|113.9985",
                "K73-D130|天恆|Tin Heng|22.469006|113.999661"
            }},
            // K74 Circular
            { "K74_O", new string[] {
                "K74-U005|天水圍市中心|Tin Shui Wai Town Centre|22.4585|114.0025",
                "K74-U010|天瑞|Tin Shui|22.4565|113.9985",
                "K74-U030|天水圍公園|Tin Shui Wai Park|22.4558|114.0025",
                "K74-U050|天慈邨|Tin Tsz Estate|22.4535|114.0068",
                "K74-U060|朗屏邨|Long Ping Estate|22.4495|114.0238",
                "K74-U090|元朗大棠路|Yuen Long Tai Tong Road|22.4445|114.0295",
                "K74-U105|港鐵元朗站|MTR Yuen Long Station|22.4461|114.0345",
                "K74-U115|博愛醫院 (凹頭)|Pok Oi Hospital (Au Tau)|22.4445|114.0415",
                "K74-U130|港鐵朗屏站|MTR Long Ping Station|22.4478|114.0252",
                "K74-U150|天盛苑|Tin Shing Court|22.4458|114.0015",
                "K74-U180|天瑞|Tin Shui|22.4565|113.9985",
                "K74-U200|天水圍市中心|Tin Shui Wai Town Centre|22.4585|114.0025"
            }},
            // K75A Circular
            { "K75A_O", new string[] {
                "K75A-U010|天水圍站|Tin Shui Wai Station|22.446556|114.003731",
                "K75A-U020|天盛苑|Tin Shing Court|22.4458|114.0015",
                "K75A-U050|廈村|Ha Tsuen|22.4445|113.9945",
                "K75A-U080|洪福邨|Hung Fuk Estate|22.4335|113.9985",
                "K75A-U100|洪水橋站|Hung Shui Kiu Stop|22.4338|113.9975",
                "K75A-U130|天盛苑|Tin Shing Court|22.4458|114.0015",
                "K75A-U140|天水圍站|Tin Shui Wai Station|22.446556|114.003731"
            }},
            // K75P Circular
            { "K75P_O", new string[] {
                "K75P-U010|天瑞|Tin Shui|22.4565|113.9985",
                "K75P-U030|天水圍公園|Tin Shui Wai Park|22.4558|114.0025",
                "K75P-U050|天耀邨|Tin Yiu Estate|22.4505|114.0028",
                "K75P-U060|天水圍站|Tin Shui Wai Station|22.446556|114.003731",
                "K75P-U110|洪福邨|Hung Fuk Estate|22.4335|113.9985",
                "K75P-U130|洪水橋站|Hung Shui Kiu Stop|22.4338|113.9975",
                "K75P-U180|天盛苑|Tin Shing Court|22.4458|114.0015",
                "K75P-U220|天瑞|Tin Shui|22.4565|113.9985"
            }},
            // K75S Circular
            { "K75S_O", new string[] {
                "K75S-U010|天水圍站|Tin Shui Wai Station|22.446556|114.003731",
                "K75S-U020|天盛苑|Tin Shing Court|22.4458|114.0015",
                "K75S-U040|洪福邨|Hung Fuk Estate|22.4335|113.9985",
                "K75S-U060|天盛苑|Tin Shing Court|22.4458|114.0015",
                "K75S-U070|天水圍站|Tin Shui Wai Station|22.446556|114.003731"
            }},
            // K76 Outbound & Inbound (Exact 4 stops each)
            { "K76_O", new string[] {
                "K76-U010|天恆|Tin Heng|22.469006|113.999661",
                "K76-U020|天逸邨逸潭樓|Yat Tam House Tin Yat Estate|22.468178|113.999383",
                "K76-U030|天富苑欣富閣|Yan Fu House Tin Fu Court|22.4638|113.9985",
                "K76-U040|天盛苑|Tin Shing Court|22.4458|114.0015",
                "K76-U050|天水圍站|Tin Shui Wai Station|22.446556|114.003731"
            }},
            { "K76_I", new string[] {
                "K76-D010|天水圍站 (港鐵天水圍站)|Tin Shui Wai Station|22.446556|114.003731",
                "K76-D020|天恩邨|Tin Yan Estate|22.4632|113.9968",
                "K76-D030|天澤邨|Tin Chak Estate|22.4672|113.9982",
                "K76-D040|天恆|Tin Heng|22.469006|113.999661"
            }},
            // 506 Circular
            { "506_O", new string[] {
                "506-U010|屯門碼頭|Tuen Mun Ferry Pier|22.372986|113.966502",
                "506-U020|美樂花園|Melody Garden|22.3752|113.9625",
                "506-U030|蝴蝶站|Butterfly Stop|22.3782|113.9635",
                "506-U050|龍門站|Lung Mun Stop|22.3852|113.9685",
                "506-U075|港鐵屯門站|MTR Tuen Mun Station|22.3952|113.9731",
                "506-U090|屯門市中心|Tuen Mun Town Centre|22.3918|113.9765",
                "506-U100|安定邨|On Ting Estate|22.3882|113.9768",
                "506-U110|兆麟苑|Siu Lun Court|22.3842|113.9748",
                "506-U120|豐景園|Goodview Garden|22.3835|113.9728",
                "506-U150|屯門碼頭|Tuen Mun Ferry Pier|22.372986|113.966502"
            }}
        };

        public static void ClearCache()
        {
        }

        public static Task EnsureOpenDataCsvLoadedAsync()
        {
            return Task.FromResult(0);
        }

        public static Task AddCatalogRoutesAsync(List<NearbyRouteItem> catalog)
        {
            AddCatalogRoutes(catalog);
            return Task.FromResult(0);
        }

        public static void AddCatalogRoutes(List<NearbyRouteItem> catalog)
        {
            for (int i = 0; i < _catalogTable.GetLength(0); i++)
            {
                string code = _catalogTable[i, 0];
                string dir = _catalogTable[i, 1];
                string oTc = _catalogTable[i, 2];
                string oEn = _catalogTable[i, 3];
                string dTc = _catalogTable[i, 4];
                string dEn = _catalogTable[i, 5];
                bool isTwoWay = (_catalogTable[i, 6] == "1");
                string fare = _catalogTable[i, 7];

                catalog.Add(new NearbyRouteItem
                {
                    RouteId = string.Format("MTRB_{0}_{1}", code, dir),
                    RouteNumber = code,
                    Direction = dir,
                    ServiceType = "1",
                    CompanyTc = "港鐵巴士",
                    CompanyEn = "mtr bus",
                    TransportType = "巴士",
                    HasOppositeDirection = isTwoWay,
                    OriginTc = oTc,
                    OriginEn = oEn,
                    DestinationTc = dTc,
                    DestinationEn = dEn,
                    StopNameTc = oTc,
                    StopNameEn = oEn,
                    FareText = string.Format("({0}, $2.0)", fare)
                });
            }
        }

        // Uses the hardcoded real-world stop table (never deletes Stop #1 or shifts stop names) and overlays live getSchedule ETAs by StopId
        public static async Task PopulateRouteStopsAndLiveEtasAsync(NearbyRouteItem route)
        {
            if (route == null) return;

            string codeUpper = route.RouteNumber.Trim().ToUpperInvariant();
            string dir = (route.Direction == "I") ? "I" : "O";
            string key = codeUpper + "_" + dir;
            if (!_hardcodedRouteStops.ContainsKey(key))
            {
                key = codeUpper + "_O";
            }

            // Update route Origin/Destination headers to match the active direction
            for (int i = 0; i < _catalogTable.GetLength(0); i++)
            {
                if (_catalogTable[i, 0] == codeUpper && _catalogTable[i, 1] == dir)
                {
                    route.OriginTc = _catalogTable[i, 2];
                    route.OriginEn = _catalogTable[i, 3];
                    route.DestinationTc = _catalogTable[i, 4];
                    route.DestinationEn = _catalogTable[i, 5];
                    route.HasOppositeDirection = (_catalogTable[i, 6] == "1");
                    break;
                }
            }

            Dictionary<string, JsonObject> liveScheduleByStopId = new Dictionary<string, JsonObject>();
            try
            {
                string lang = ThemeManager.IsEnglish ? "en" : "zh";
                string payload = string.Format("{{\"language\":\"{0}\",\"routeName\":\"{1}\"}}", lang, codeUpper);
                string jsonText = await BusRouteDatabase.HttpPostStringAsync(MtrBusScheduleUrl, payload);

                if (!string.IsNullOrEmpty(jsonText))
                {
                    JsonObject root = JsonObject.Parse(jsonText);
                    if (root.ContainsKey("busStop") && root["busStop"].ValueType == JsonValueType.Array)
                    {
                        JsonArray stopArr = root["busStop"].GetArray();
                        for (int i = 0; i < stopArr.Count; i++)
                        {
                            JsonObject sObj = stopArr[i].GetObject();
                            string stopId = BusRouteDatabase.GetJsonString(sObj, "busStopId");
                            if (!string.IsNullOrEmpty(stopId))
                            {
                                liveScheduleByStopId[stopId] = sObj;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            bool isTaiPoFeeder = (codeUpper == "K12" || codeUpper == "K14" || codeUpper == "K17" || codeUpper == "K18");
            string routeFare = isTaiPoFeeder ? "$4.8" : "$5.1";

            List<BusStopItem> stops = new List<BusStopItem>();
            if (_hardcodedRouteStops.ContainsKey(key))
            {
                string[] rawStops = _hardcodedRouteStops[key];
                for (int i = 0; i < rawStops.Length; i++)
                {
                    string[] parts = rawStops[i].Split('|');
                    string stopId = parts[0];
                    string nameTc = parts[1];
                    string nameEn = parts[2];
                    double lat = double.Parse(parts[3], CultureInfo.InvariantCulture);
                    double lon = double.Parse(parts[4], CultureInfo.InvariantCulture);
                    int seq = i + 1;

                    List<int> mins = new List<int>();
                    List<string> rmks = new List<string>();
                    if (liveScheduleByStopId.ContainsKey(stopId))
                    {
                        ExtractMinsAndRemarksFromBusStopObject(liveScheduleByStopId[stopId], mins, rmks);
                    }

                    stops.Add(new BusStopItem
                    {
                        RouteId = route.RouteId,
                        StopId = stopId,
                        Sequence = seq,
                        StopNameTc = string.Format("{0} ({1})", nameTc, stopId),
                        StopNameEn = string.Format("{0} ({1})", nameEn, stopId),
                        NormalFare = routeFare,
                        JoyYouFare = "$2.0",
                        IsSectionFare = false,
                        HasTimetable = true,
                        Latitude = lat,
                        Longitude = lon,
                        LiveEtaMinutes = mins,
                        LiveEtaRemarks = rmks,
                        IsFavouriteStop = ThemeManager.IsFavourite(route.RouteId, stopId, seq),
                        IsExpanded = false
                    });
                }
            }

            if (stops.Count > 0)
            {
                int expandIdx = GeoMapService.SelectNearestOrFirstStopIndex(stops, route.NearestStopId);
                stops[expandIdx].IsExpanded = true;
                route.Stops = stops;
            }
        }

        public static async Task FetchSingleStopEtaLiveAsync(NearbyRouteItem route, BusStopItem stop)
        {
            if (route == null || stop == null) return;

            try
            {
                string lang = ThemeManager.IsEnglish ? "en" : "zh";
                string payload = string.Format("{{\"language\":\"{0}\",\"routeName\":\"{1}\"}}", lang, route.RouteNumber);
                string jsonText = await BusRouteDatabase.HttpPostStringAsync(MtrBusScheduleUrl, payload);

                if (!string.IsNullOrEmpty(jsonText))
                {
                    JsonObject root = JsonObject.Parse(jsonText);
                    if (root.ContainsKey("busStop") && root["busStop"].ValueType == JsonValueType.Array)
                    {
                        JsonArray stopArr = root["busStop"].GetArray();
                        List<int> mins = new List<int>();
                        List<string> rmks = new List<string>();

                        for (int i = 0; i < stopArr.Count; i++)
                        {
                            JsonObject sObj = stopArr[i].GetObject();
                            if (BusRouteDatabase.GetJsonString(sObj, "busStopId") == stop.StopId)
                            {
                                ExtractMinsAndRemarksFromBusStopObject(sObj, mins, rmks);
                                break;
                            }
                        }

                        stop.LiveEtaMinutes = mins;
                        stop.LiveEtaRemarks = rmks;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static void ExtractMinsAndRemarksFromBusStopObject(JsonObject sObj, List<int> mins, List<string> rmks)
        {
            if (sObj == null || !sObj.ContainsKey("bus") || sObj["bus"].ValueType != JsonValueType.Array) return;

            JsonArray busArr = sObj["bus"].GetArray();
            for (int b = 0; b < busArr.Count; b++)
            {
                JsonObject bObj = busArr[b].GetObject();
                string secStr = BusRouteDatabase.GetJsonString(bObj, "arrivalTimeInSecond");
                if (string.IsNullOrEmpty(secStr) || secStr == "0" || secStr == "-")
                {
                    secStr = BusRouteDatabase.GetJsonString(bObj, "departureTimeInSecond");
                }

                int secs = -1;
                if (int.TryParse(secStr, out secs) && secs >= 0 && secs <= 3600 && mins.Count < 3)
                {
                    mins.Add(secs / 60);
                    string rmk = BusRouteDatabase.SanitizeEtaRemark(BusRouteDatabase.GetJsonString(bObj, "busRemark"));
                    if (string.IsNullOrEmpty(rmk)) rmk = ThemeManager.Tr("港鐵巴士", "mtr bus");
                    rmks.Add(rmk);
                }
            }
        }
    }
}