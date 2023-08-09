#!/usr/bin/env python3
from datetime import datetime
import os
import requests
import json


def create_footer(json_object: dict) -> str:
    return """</channel>
</rss>"""


def get_content_length(content_url: str) -> int:
    with requests.get(content_url, stream=True) as response:
        content_length = response.headers.get("Content-length")
    if content_length is not None:
        return int(content_length)


def get_guid(content_url: str) -> str:
    """"Gets the guid from the content url"""
    return content_url.split("/")[-1].split("_0_")[0]


def format_date(datestring: str) -> str:
    """Takes the nrk publishedAt date and returns a valid podcast RFC-822 date-time"""
    return (datetime.fromisoformat(datestring.strip("Z")+".000000").strftime("%a, %d %b %Y %X %Z") + "GMT")


def sec_to_hour_min_sec(sec: int):
    hours = sec//3600
    min = (sec-hours*3600)//60
    sec = ((sec-(hours*3600+min*60)))

    return f"{(str.zfill(str(hours),2)+':')}{(str.zfill(str(min),2)+':')}{str.zfill(str(sec),2)}"


def pt_string_to_duration_str(pt_string: str) -> str:
    if not pt_string.startswith("PT"):
        raise ValueError("String did not start with 'PT'")

    pt_string = pt_string.strip("PT")


def replace_illegal_chars(string: str) -> str:
    return string.replace("&", "&amp;").replace("<", " &lt;").replace(">", "&gt;").replace("'", "&apos;").replace('"', "&quot;")


class EpisodeItem:
    def __init__(self, itemjson: dict, content_length=None) -> None:
        self.title: str = replace_illegal_chars(
            itemjson.get("titles").get("title").strip())
        self.description: str = replace_illegal_chars(
            itemjson.get("titles").get("subtitle").strip())
        self.datetime_string: str = format_date(itemjson.get("publishedAt"))
        self.content_url = replace_illegal_chars(
            itemjson.get("downloadables")[0].get("audio").get("url"))
        # if content_length is not None:
        #     self.content_length = content_length
        # else:
        #     self.content_length = get_content_length(self.content_url)
        self.guid = get_guid(self.content_url)
        self.bitrate = 192_000 if "192" in self.content_url else 128_000
        # self.duration_str = f"{sec_to_hour_min_sec((self.content_length*8)//(self.bitrate))}"
        self.duration_str = itemjson.get("duration")

    @classmethod
    def from_json(_class, json: dict):
        nrk_dict = {'titles': {'title': json["title"]}}
        # return _class(json)


def json_to_episodeitem(json_items_object: list[dict]) -> list[EpisodeItem]:
    print(f"Converting to EpisodeItem")
    episodeitems: list[EpisodeItem] = []
    for index, item in enumerate(json_items_object):
        #print(f"Item {index+1} of {len(json_items_object)}")
        episodeitems.append(EpisodeItem(item))
    return episodeitems


def save_ep_items(epitems: list[EpisodeItem], show_title: str):
    jsonstring = ""
    for ep in epitems:
        jsonstring += json.dumps(ep.__dict__) + ',\n'
    jsonstring = jsonstring[:-2]
    jsonstring += ""
    return jsonstring
    a = json.loads(jsonstring.split(',\n')[0])
    # print(a["title"])


def create_header(json_object: dict):
    return f"""<?xml version="1.0" encoding="UTF-8"?>
    <rss version="2.0"
        xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd"
        xmlns:android="http://schemas.android.com/apk/res/android">
    <channel>
        <title>CUSTOM: {json_object.get("_embedded").get("podcast").get("titles").get("title").strip().replace("&","&amp;")}</title>
        <link>{json_object.get("_embedded").get("podcast").get("_links").get("podcast").get("href")}</link>
        <description>{json_object.get("_embedded").get("podcast").get("titles").get("subtitle")}</description>
        <language>no</language>
        <copyright>NRK © 2022</copyright>
        <category>Comedy</category>
        <image>
        <title>{json_object.get("_embedded").get("podcast").get("titles").get("title").strip().replace("&", "&amp;")}</title>
        <url>{json_object.get("_embedded").get("podcast").get("imageUrl")}</url>
        <link>{json_object.get("_embedded").get("podcast").get("_links").get("podcast").get("href")}</link>
        <width>144</width>
        <height>144</height>
        </image> 
        <!-- <itunes:author>BB</itunes:author> -->
        <!-- <description>Custom up to date {json_object.get("_embedded").get("podcast").get("titles").get("title").strip()} feed</description> -->
        <!--<itunes:image href="https://gfx.nrk.no/1J1itjoQOpBLfHDw3Y7FUQyhLJqIrokksb5LOB4IgvDw.png"/>-->"""


def create_feed(url: str) -> str:
    response = requests.get(url)
    if response.ok:
        full_rss_feed = ""
        json_object = json.loads(response.text)
        full_rss_feed += create_header(json_object)
        full_rss_feed += create_episode_items(json_object)
        full_rss_feed += create_footer(json_object)
    return full_rss_feed


def create_episode_items(json_object: dict) -> str:
    number_of_episodes = json_object.get(
        "_embedded").get("podcast").get("episodeCount")
    if number_of_episodes != len(json_object.get("items")):
        print("Wrong length of items list!")
        exit(99)
    itemstring = ""
    # if os.path.exists(f'{json_object.get("_embedded").get("podcast").get("titles").get("title").strip()}.epitems'):
    #     epitems = load_ep_items(f'{json_object.get("_embedded").get("podcast").get("titles").get("title").strip()}.epitems')
    items = json_to_episodeitem(json_object.get("items"))
    # load_ep_items(save_ep_items(items, json_object.get("_embedded").get("podcast").get("titles").get("title").strip()))
    for index, EpItem in enumerate(items):
        print(f"\ritem: {index+1} of {number_of_episodes}")
        itemstring += f"""<item>
            <title>{EpItem.title}</title>
            <description>{EpItem.description}</description>
            <pubDate>{EpItem.datetime_string} </pubDate> <!-- Tue, 11 Oct 2022 02:00:00 GMT -->
            <enclosure url="{EpItem.content_url}" type="audio/mpeg"/>
            <itunes:duration>{EpItem.duration_str}</itunes:duration>
            <guid isPermaLink="false">{EpItem.guid}</guid>
        </item>
        """
        # length="{EpItem.content_length}"
    return itemstring


if __name__ == "__main__":
    #os.chdir(os.path.dirname(os.path.abspath(__file__)))
    podnames = ["berrum_beyer_snakker_om_greier", "abels_taarn",
                "hele_historien", "trygdekontoret", "loerdagsraadet"]
    # if len(sys.argv) > 1:
    #     name = sys.argv[1]
    for pod in podnames:
        url = f"https://psapi.nrk.no/podcasts/{pod}/episodes?cursor=2130-06-17T18%3A00%3A00Z&Direction=backwards&PageCount=1000"
        rss_feed = create_feed(url)
        with open(f"{pod}.rss", 'w', encoding="utf-8") as feedfile:
            feedfile.write(rss_feed)
