using API.DTOs;
using API.Entity;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace API.Controllers
{
    public class MLController : BaseAPIController
    {
        private readonly IMapper _mapper;
        private readonly IPhotoService _photoService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMLService _mLService;
        private CustomVisionTrainingClient trainingApi;
        private CustomVisionPredictionClient predictionApi;
        private static Guid projectId = Guid.Parse("c13464db-3ccd-4c23-a1e0-a1682afa0f0c");
        private static Iteration iteration;
        private static string publishedModelName = "faceClassModel";
        private static MemoryStream testImage;
        
        public MLController(IMapper mapper
            , IPhotoService photoService
            , IUnitOfWork unitOfWork
            , IMLService mLService)
        {
            _unitOfWork = unitOfWork;
            _mLService = mLService;
            _photoService = photoService;
            _mapper = mapper;
            
            trainingApi =  _mLService.AuthenticateTraining();
            predictionApi = _mLService.AuthenticatePrediction();
        }
               
        [HttpGet("Create")]
        public Guid Create(){
            Console.WriteLine("Creating new project:");
            Project project = trainingApi.CreateProject("My New Project");
            return project.Id;     
        }

        [HttpGet("Train")]
        public string training(){
            
            _mLService.CreateProject(trainingApi);
            
            return "success";
        }

        [HttpGet("Predict")]
        public void predict(string fullPath){
            testImage = new MemoryStream(System.IO.File.ReadAllBytes(fullPath));
            
            Console.WriteLine("Making a prediction:");

            var result = predictionApi.ClassifyImage(projectId, publishedModelName, testImage);
        }
        [HttpGet("Delete")]
        public string Delete(){
            // Console.WriteLine("Unpublishing iteration.");
            // trainingApi.UnpublishIteration(projectId, iteration.Id);

            // Console.WriteLine("Deleting project.");
            // trainingApi.DeleteProject(projectId);
            // return("Project Deleted");
            _mLService.DeleteImages(trainingApi);
            return "success";
        }
    
        [HttpPost("add-photo")]
        [ActionName(nameof(AddPhoto))]
        public async Task<ActionResult<PhotoDTO>> AddPhoto(IFormFile file)
        {

            var username = User.GetUsername();
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(username);

            var dbPath = await _photoService.AddPhotoAsync(file, username);

            var photo = new Photo
            {
                Url = dbPath
            };
            
            if (user.Photos.Count == 0)
            {
                photo.IsMain = true;     
            }
            // Analyze attractiveness
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), photo.Url);
            var result = _mLService.TestIteration(predictionApi, fullPath);
            user.Attractiveness = result.Predictions[0].TagName;
            _unitOfWork.UserRepository.Update(user);

            user.Photos.Add(photo);

            if (await _unitOfWork.Complete())
            {
                // return "Sucess";
            }
            return BadRequest("Problem addding photo");
        }
    }
}