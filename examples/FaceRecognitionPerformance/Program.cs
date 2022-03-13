using FaceRecognitionDotNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FaceRecognitionPerformance
{
    class Program
    {
        //Photo faces from http://mmlab.ie.cuhk.edu.hk/projects/CelebA.html (no form to fullfill)

        private const string FaceModelsPath = @"D:\FaceModels";
        private const int maxImagesToLoad = 10000;
        private const double tolerance = 0.5;

        private static Dictionary<string, (string FileName, FaceEncoding FaceEncoding)> faceEncodings = new Dictionary<string, (string, FaceEncoding)>();
        private static Dictionary<string, (string Id, FaceEncoding FaceEncoding)> testEncodings = new Dictionary<string, (string, FaceEncoding)>();
        private static Dictionary<int, FaceRecognition> frs = new Dictionary<int, FaceRecognition>();
        static void Main(string[] args)
        {
            var option = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            var identities = File.ReadAllLines($@"{FaceModelsPath}\identity_CelebA.txt").ToDictionary(k => k.Split(' ')[0], v => v.Split(' ')[1]);

            if (!File.Exists($@"{FaceModelsPath}\FaceEncodings.dat"))
            {
                Parallel.ForEach(Directory.GetFiles(@"D:\img_align_celeba").Take(maxImagesToLoad), option, file =>
                  {
                      var id = Thread.CurrentThread.ManagedThreadId;

                      FaceRecognition fr;

                      if (frs.ContainsKey(id))
                          fr = frs[id];
                      else
                      {
                          fr = FaceRecognition.Create(FaceModelsPath);
                          frs.Add(id, fr);
                      }

                      ProcessImage(identities, fr, file);
                  });

                File.WriteAllText($@"{FaceModelsPath}\FaceEncodings.dat", JsonConvert.SerializeObject(faceEncodings));
                File.WriteAllText($@"{FaceModelsPath}\TestFaceEncodings.dat", JsonConvert.SerializeObject(testEncodings));
            }

            faceEncodings = JsonConvert.DeserializeObject<Dictionary<string, (string FileName, FaceEncoding FaceEncoding)>>(File.ReadAllText($@"{FaceModelsPath}\FaceEncodings.dat"));
            testEncodings = JsonConvert.DeserializeObject<Dictionary<string, (string Id, FaceEncoding FaceEncoding)>>(File.ReadAllText($@"{FaceModelsPath}\TestFaceEncodings.dat"));

            int successful = 0;
            int falsePositive = 0;
            int falseNegative = 0;

            foreach (var encodingToCheck in testEncodings)
            {
                var distances = FaceRecognition.FaceDistances(faceEncodings.Values.Select(i => i.FaceEncoding), encodingToCheck.Value.FaceEncoding);

                var min = distances
                    .Select((v, index) => new { v, index })
                    .FirstOrDefault(v => v.v == distances.Min());

                var identityToCheck = encodingToCheck.Value.Id;

                var identity = faceEncodings.Keys.ToList()[min.index];

                if (identityToCheck == identity)
                {
                    Console.Write($"Face recognition successful for identity {identity}");

                    if (min.v <= tolerance)
                    {
                        Console.WriteLine($" - distance {min.v} - {faceEncodings[identity].FileName} {encodingToCheck.Key}");
                        successful++;
                    }
                    else
                    {
                        Console.WriteLine($" - distance {min.v} - {faceEncodings[identity].FileName} {encodingToCheck.Key} but above tolerance");
                        falseNegative++;
                    }
                }
                else
                {
                    Console.Write($"Face recognition failed for identity {identity}");

                    if (min.v <= tolerance)
                    {
                        Console.WriteLine($" - distance {min.v} - {faceEncodings[identity].FileName} {encodingToCheck.Key} ************ DANGER !");
                        falsePositive++;
                    }
                    else
                        Console.WriteLine($" - distance {min.v} - {faceEncodings[identity].FileName} {encodingToCheck.Key}");
                }
            }

            Console.WriteLine($"Face recognition successful for {successful} / {testEncodings.Count()} = {successful / (double)testEncodings.Count():P2}");
            Console.WriteLine($"Danger: false positive {falsePositive}");
            Console.WriteLine($"Oops: false negative {falseNegative}");
        }
        private static void ProcessImage(Dictionary<string, string> identities, FaceRecognition fr, string file)
        {
            using (var image = FaceRecognition.LoadImageFile(file))
            {
                var encodings = fr.FaceEncodings(image);

                Console.WriteLine($"FaceEncodings completed for {file}");

                if (encodings.Count() == 0)
                    Console.WriteLine($"No face detected in {file}");
                else if (encodings.Count() > 1)
                    Console.WriteLine($"Too many faces detected in {file}");
                else
                {
                    var fileName = Path.GetFileName(file);

                    var id = identities[fileName];

                    var encoding = encodings.Single();

                    if (faceEncodings.ContainsKey(id))
                    {
                        Console.WriteLine($"Face encoding for identity {id} already present");

                        testEncodings.Add(fileName, (Id: id, FaceEncoding: encoding));
                    }
                    else
                        faceEncodings.Add(id, (FileName: fileName, FaceEncoding: encoding));
                }
            }
        }
    }
}
