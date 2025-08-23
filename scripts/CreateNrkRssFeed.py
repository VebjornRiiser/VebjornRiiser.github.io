#!/usr/bin/env python3
import json
import os
import sys
import time

import requests

# 'User-Agent': "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/117.0",
PODLISTFILENAME = "PodcastsToUpdate.txt"
RequestHeaders = {"Accept": "*/*",
                  "Accept-Language": "en-US,en;q=0.5", }


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
    return content_url.split("/")[-1].split("_0_")[0].split(".mp3")[0]


def format_date(datestring: str) -> str:
    """Takes the nrk publishedAt date and returns a valid podcast RFC-822 date-time"""
    return datestring
    # return (datetime.fromisoformat(datestring.strip("Z")+".000000").strftime("%a, %d %b %Y %X %Z") + "GMT")


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


def get_podcast_url_name(episode_item):
    return episode_item.get("_links").get("series").get("name")


def get_content_url(episode_item):
    return f"https://podkast.nrk.no/fil/{get_podcast_url_name(episode_item)}/{get_episode_id(episode_item)}_0_ID192MP3.mp3"

class EpisodeItem:
    def __init__(self, itemjson: dict, playback_url: list[str], content_length=None) -> None:
        self.title: str = replace_illegal_chars(
            itemjson.get("titles").get("title").strip())
        self.description: str = replace_illegal_chars(
            itemjson.get("titles").get("subtitle").strip())
        self.datetime_string: str = format_date(itemjson.get("date"))
        self.episode_image_url = itemjson.get("squareImage")[-1].get("url")
        self.content_url = playback_url
        if content_length is not None:
            self.content_length = content_length
        else:
            pass
            # print("Did not get length from api")
            # self.content_length = get_content_length(self.content_url)
        self.guid = get_guid(self.content_url)
        self.bitrate = 192_000 if "192" in self.content_url else 128_000
        # self.duration_str = f"{sec_to_hour_min_sec((self.content_length*8)//(self.bitrate))}"
        self.duration_str = itemjson.get("duration")

    @classmethod
    def from_json(_class, json: dict):
        nrk_dict = {'titles': {'title': json["title"]}}


def json_to_episodeitem(episodes, playback_list) -> list[EpisodeItem]:
    episodeitems: list[EpisodeItem] = []
    try:
        for item, playback_url in zip(episodes, playback_list):
            episodeitems.append(EpisodeItem(item, playback_url))
        return episodeitems
    except Exception as e:
        print(episodes)


def save_ep_items(epitems: list[EpisodeItem], show_title: str):
    jsonstring = ""
    for ep in epitems:
        jsonstring += json.dumps(ep.__dict__) + ',\n'
    jsonstring = jsonstring[:-2]
    jsonstring += ""
    return jsonstring


def create_header(episode: dict):
    return f"""<?xml version="1.0" encoding="UTF-8"?>
    <rss version="2.0"
        xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd"
        xmlns:android="http://schemas.android.com/apk/res/android">
    <channel>
        <title>CUSTOM: {get_show_title(episode)}</title>
        <link>{get_show_link(episode)}</link>
        <!--<description>{get_description(episode)}</description>-->
        <language>no</language>
        <copyright>NRK © 2022</copyright>
        <category>Comedy</category>
        <image>
        <title>{get_show_title(episode)}</title>
        <url>{get_show_image(episode)}</url>
        <link>{get_show_link(episode)}</link>
        <width>144</width>
        <height>144</height>
        </image>
        """


def get_episodes_list(json_object):
    return json_object.get("_embedded").get("episodes")


def get_show_title(json_object):
    return json_object.get("originalTitle").strip().replace("&", "&amp;")


def get_episode_link(episode):
    return episode.get("_links").get("share").get("href")


def get_show_link(episode):
    link = get_episode_link(episode)
    return "/".join(link.split("/")[0:-1])


def get_episode_id(episode_item):
    return episode_item.get("episodeId")


def get_description(json_object):
    # or maybe not. check under titles
    return ""  # need to get separate file https://psapi.nrk.no/playback/metadata/podcast/berrum_beyer_snakker_om_greier/l_911b3bdf-2979-40df-9b3b-df297950dfef


def get_show_image(episode):
    return episode.get("squareImage")[-1].get("url")


def create_feed(url: str) -> str:
    # response = requests.get(url)
    # if response.ok:
    full_rss_feed = ""
    # json_object = None
    # with open("./scripts/episodes.json", 'r', encoding="utf-8") as episode_json_file:
    #     json_object = json.loads(episode_json_file.read())

    episode_json_list = get_all_episode_items(url)
    # Need to get all the episodes.json until there is no more and then get all the playback pages
    full_rss_feed += create_header(episode_json_list[0])
    full_rss_feed += create_episode_items(episode_json_list)
    full_rss_feed += create_footer(episode_json_list[0])
    # else:
    #     print(f"Bad response from {url}. Got {response.status_code}")
    return full_rss_feed



def get_all_episode_items(base_url: str, requests_per_second=2):
    more_episodes_to_get = True
    page_index = 1
    episode_json_list: list[str] = []
    while more_episodes_to_get:
        url = base_url+str(page_index)

        response = requests.get(url, headers=RequestHeaders)

        response.raise_for_status()
        print("At page index ", page_index)

        episode_json_list.extend(get_episodes_list(json.loads(response.content)))

        if get_number_of_episodes_from_episode_json(response.text) != 50:
            break
        page_index += 1
        time.sleep(1/requests_per_second)

    return episode_json_list


def get_number_of_episodes_from_episode_json(json_str: str) -> int:
    object: dict = json.loads(json_str)
    eps = object.get("_embedded").get("episodes")
    return len(eps)


def get_all_playback_urls(episode_list: list[str], requests_per_sec=20) -> list[str]:
    EpisodeResults: list[requests.Response] = []
    urls_to_fetch = [
        f"https://psapi.nrk.no/playback/manifest/podcast/{get_episode_id(episode)}" for episode in episode_list]
    with requests.session() as sess:
        for url in urls_to_fetch:
            response: requests.Response = sess.get(url)
            response.raise_for_status()
            EpisodeResults.append(response)
            time.sleep(1/requests_per_sec)

    playback_urls: list[str] = []
    for response in EpisodeResults:
        if response.status_code != 200:
            raise Exception("Got status code 200.")

        playback_urls.append(get_playback_url(response.text))
    return playback_urls


def get_playback_url(json_str):
    return json.loads(json_str).get("playable").get("assets")[0].get("url")


def create_episode_items(episodes: dict) -> str:
    print("Getting playback urls")
    playback_urls = get_all_playback_urls(episodes)

    items = json_to_episodeitem(episodes, playback_urls)

    return generate_episode_rss(items)


def generate_episode_rss(items):
    itemstring = ""
    for EpItem in items:
        itemstring += f"""
        <item>
            <title>{EpItem.title}</title>
            <description>{EpItem.description}</description>
            <pubDate>{EpItem.datetime_string} </pubDate>
            <enclosure url="{EpItem.content_url}" type="audio/mpeg"/>
            <itunes:duration>{EpItem.duration_str}</itunes:duration>
            {f'<itunes:image href="{EpItem.episode_image_url}"/>' if EpItem.episode_image_url is not None else ''}
            <guid isPermaLink="false">{EpItem.guid}</guid>
        </item>
        """
    return itemstring


if __name__ == "__main__":
    podnames = []
    # podnames = [
    # "berrum_beyer_snakker_om_greier",
    # "trygdekontoret",
    # "abels_taarn",
    # "hele_historien",
    # "debatten",
    # "radio_moerch",
    # "baade_erlend_og_steinar_",
    # "monsens_univers",
    # ]

    print("Trying to read settings file")
    try:
        
        full_path_to_podnames = os.path.join(os.path.dirname(__file__), PODLISTFILENAME)
        if (os.path.exists(full_path_to_podnames)):
            print(f"Found {PODLISTFILENAME}. Using to lookup podcasts")
            with open(full_path_to_podnames, 'r') as PodcastListFile:
                podnames = [name.strip() for name in PodcastListFile.readlines()]
        else:
            print(f"Did not find any settings file in '{full_path_to_podnames}'")

    except Exception as e:
        print(f"Failed to get dirname. '{e}'")


    for pod in podnames:
        print(f"Getting rss feed for '{pod}'")
        baseurl = f"https://psapi.nrk.no/radio/catalog/podcast/{pod}/episodes?pageSize=50&sort=desc&page="
        rss_feed = create_feed(baseurl)
        if rss_feed == "":
            continue
        with open(f"{pod}.rss", 'w', encoding="utf-8") as feedfile:
            feedfile.write(rss_feed)
        print(f"Done with '{pod}' sleeping before starting next")
        time.sleep(2)
