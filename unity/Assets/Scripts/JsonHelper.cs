using UnityEngine;
using System;

// 유니티 JsonUtility 배열 파싱 지원
public static class JsonHelper
{

    public static T[] FromJson<T>(string json)
    {
        string newJson = "{\"list\":" + json + "}";

        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
        return wrapper.list;
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] list;
    }
}

[Serializable]
public class YoloDetection
{
    public int[] box;
    public float confidence;
}