﻿using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using netcore_google_map.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using System.Text;

// using System.IO.TextWriter;

namespace netcore_google_map.Controllers
{
    [ApiController]
    [Route("/v1/places")]
    [Produces("application/json")]
    public class PlacesController : ControllerBase
    {

        private readonly ILogger<PlacesController> _logger;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _configuration;

        public PlacesController(ILogger<PlacesController> logger, IDistributedCache distributedCache, IConfiguration configuration)
        {
            _logger = logger;
            _distributedCache = distributedCache;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<ActionResult<List<PlaceResponseList>>> List([FromQuery]string keyword)
        {
            List<PlaceResponseList> place = null;

            // Get Redis cache
            var inCache = _distributedCache.GetString(keyword);
            if (!string.IsNullOrEmpty(inCache)) {
                var getData = await _distributedCache.GetAsync(keyword);
                var bytesAsString = Encoding.UTF8.GetString(getData);
                place = JsonConvert.DeserializeObject<List<PlaceResponseList>>(bytesAsString);

                return Ok(place);
            }   
           
            // Get Google map api
            var gmapKey = _configuration["GoogleMapsApi:Key"];
            var gmapUrl = _configuration["GoogleMapsApi:Url"];               
            var gmapPlaceTextSearch = _configuration["GoogleMapsApi:PlaceTextSearchUrl"];     

            string googleApiUrl = gmapUrl + gmapPlaceTextSearch;     
            googleApiUrl = googleApiUrl.Replace("{keyword}", keyword); 
            googleApiUrl = googleApiUrl.Replace("{gmapKey}", gmapKey); 

            var client = new HttpClient();
            var result = await client.GetStringAsync(String.Format(googleApiUrl));        

            var jsonObject = JsonConvert.DeserializeObject<RootResultList>(result);
            place = jsonObject.results;
            
            // Set Redis cache
            var expirationMinutes = Convert.ToDouble(_configuration["Redis:ExpirationMinutes"]);
            string serializeObject = JsonConvert.SerializeObject(place);
            byte[] data = Encoding.UTF8.GetBytes(serializeObject);
            await _distributedCache.SetAsync(keyword, data, new DistributedCacheEntryOptions()
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(expirationMinutes)
            });

            return Ok(place);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PlaceResponseShow>> Show(string id)
        {
            PlaceResponseShow place = null;

            // Get Redis cache
            var inCache = _distributedCache.GetString(id);
            if (!string.IsNullOrEmpty(inCache)) {
                var getData = await _distributedCache.GetAsync(id);
                var bytesAsString = Encoding.UTF8.GetString(getData);
                place = JsonConvert.DeserializeObject<PlaceResponseShow>(bytesAsString);

                return Ok(place);
            }   
           
            // Get Google map api
            var gmapKey = _configuration["GoogleMapsApi:Key"];
            var gmapUrl = _configuration["GoogleMapsApi:Url"];               
            var gmapPlaceDetail = _configuration["GoogleMapsApi:PlaceDetail"];     

            string googleApiUrl = gmapUrl + gmapPlaceDetail;     
            googleApiUrl = googleApiUrl.Replace("{placeId}", id); 
            googleApiUrl = googleApiUrl.Replace("{gmapKey}", gmapKey); 

            var client = new HttpClient();
            var result = await client.GetStringAsync(String.Format(googleApiUrl));        

            var jsonObject = JsonConvert.DeserializeObject<RootResult>(result);
            place = jsonObject.result;
            
            // Set Redis cache
            var expirationMinutes = Convert.ToDouble(_configuration["Redis:ExpirationMinutes"]);
            string serializeObject = JsonConvert.SerializeObject(place);
            byte[] data = Encoding.UTF8.GetBytes(serializeObject);
            await _distributedCache.SetAsync(id, data, new DistributedCacheEntryOptions()
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(expirationMinutes)
            });

            return Ok(place);
        }
    }
}
