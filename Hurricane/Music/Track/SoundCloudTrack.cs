﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CSCore;
using CSCore.Codecs;
using Exceptionless.Json;
using Hurricane.Music.MusicDatabase;
using Hurricane.Music.Track.SoundCloudApi;
using Hurricane.Settings;

namespace Hurricane.Music.Track
{
    public class SoundCloudTrack : StreamableBase
    {
        public int SoundCloudID { get; set; }
        public string Url { get; set; }
        public string ArtworkUrl { get; set; }

        public async override Task<bool> LoadInformation()
        {
            using (var web = new WebClient { Proxy = null })
            {
                var result = JsonConvert.DeserializeObject<ApiResult>(await web.DownloadStringTaskAsync(string.Format("https://api.soundcloud.com/tracks/{0}.json?client_id={1}", SoundCloudID, SensitiveInformation.SoundCloudKey)));
                return await LoadInformation(result);
            }
        }

        public async Task<bool> LoadInformation(ApiResult result)
        {
            if (!result.streamable) return false;
            Year = result.release_year != null
                ? uint.Parse(result.release_year.ToString())
                : (uint)DateTime.Parse(result.created_at).Year;
            Title = result.title;
            ArtworkUrl = result.artwork_url != null ? result.artwork_url.Replace("large.jpg", "{0}.jpg") : string.Empty;
            Artist = result.user.username;
            Genres = result.genre;

            using (var soundSource = await GetSoundSource())
            {
                kHz = soundSource.WaveFormat.SampleRate / 1000;
                SetDuration(soundSource.GetLength());
            }
            return true;
        }

        #region Image

        public async override void Load()
        {
            IsLoadingImage = true;

            if (Image == null)
            {
                var diAlbumCover = new DirectoryInfo(HurricaneSettings.Instance.CoverDirectory);
                Image = MusicCoverManager.GetSoundCloudImage(this, diAlbumCover, HurricaneSettings.Instance.Config.DownloadAlbumCoverQuality, HurricaneSettings.Instance.Config.LoadAlbumCoverFromInternet);
                if (Image == null && HurricaneSettings.Instance.Config.LoadAlbumCoverFromInternet)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(ArtworkUrl))
                        {
                            Image = await SoundCloudApi.SoundCloudApi.LoadBitmapImage(this, HurricaneSettings.Instance.Config.DownloadAlbumCoverQuality, diAlbumCover);
                        }
                        else
                        {
                            Image = await MusicCoverManager.LoadCoverFromWeb(this, diAlbumCover).ConfigureAwait(false);
                        }
                    }
                    catch (WebException)
                    {
                        //Happens, doesn't matter
                    }
                }
            }

            IsLoadingImage = false;
            OnImageLoadComplete();
        }

        #endregion

        public override Task<IWaveSource> GetSoundSource()
        {
            return
                Task.Run(() => CodecFactory.Instance.GetCodec(
                    new Uri(
                        string.Format("https://api.soundcloud.com/tracks/{0}/stream?client_id={1}", SoundCloudID,
                            SensitiveInformation.SoundCloudKey))));
        }

        public override bool Equals(PlayableBase other)
        {
            if (other == null) return false;
            if (GetType() != other.GetType()) return false;
            return SoundCloudID == ((SoundCloudTrack) other).SoundCloudID;
        }

        public override void OpenTrackLocation()
        {
            Process.Start(Url);
        }

        private static GeometryGroup _geometryGroup;
        public static GeometryGroup GetProviderVector()
        {
            if (_geometryGroup == null)
            {
                _geometryGroup = new GeometryGroup();
                _geometryGroup.Children.Add((Geometry)Application.Current.Resources["VectorSoundCloud"]);
            }
            return _geometryGroup;
        }

        public override GeometryGroup ProviderVector
        {
            get { return GetProviderVector(); }
        }
    }
}