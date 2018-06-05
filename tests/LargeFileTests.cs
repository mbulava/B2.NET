﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using B2Net.Http;
using B2Net.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace B2Net.Tests {
	[TestClass]
	public class LargeFileTests : BaseTest {
		private B2Bucket TestBucket = new B2Bucket();
		private B2Client Client = null;
		private List<B2File> FilesToDelete = new List<B2File>();
	    private string BucketName = "";

#if NETFULL
	    private string FilePath => Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "../../../");
#else
        private string FilePath => Path.Combine(System.AppContext.BaseDirectory, "../../../");
#endif

        [TestInitialize]
		public void Initialize() {
			Client = new B2Client(B2Client.Authorize(Options));
            BucketName = $"B2NETTestingBucket-{Path.GetRandomFileName().Replace(".", "").Substring(0, 6)}";
            var buckets = Client.Buckets.GetList().Result;
			B2Bucket existingBucket = null;
			foreach (B2Bucket b2Bucket in buckets) {
				if (b2Bucket.BucketName == BucketName) {
					existingBucket = b2Bucket;
				}
			}

			if (existingBucket != null) {
				TestBucket = existingBucket;
			} else {
				TestBucket = Client.Buckets.Create(BucketName, BucketTypes.allPrivate).Result;
			}
		}
        
        // THIS TEST DOES NOT PROPERLY CLEAN UP after an exception.
		[TestMethod]
		public void LargeFileUploadTest() {
			var fileName = "B2LargeFileTest.txt";
			FileStream fileStream = File.OpenRead(Path.Combine(FilePath, fileName));
            var stream = new StreamReader(fileStream);
		    char[] c = null;
            List<byte[]> parts = new List<byte[]>();
		    var shas = new List<string>();

		    while (stream.Peek() >= 0) {
		        c = new char[1024 * (5 * 1024)];
		        stream.Read(c, 0, c.Length);

		        parts.Add(Encoding.UTF8.GetBytes(c));
            }

		    foreach (var part in parts) {
		        string hash = Utilities.GetSHA1Hash(part);
                shas.Add(hash);
            }

		    B2File start = null;
		    B2File finish = null;
            try {
		        start = Client.LargeFiles.StartLargeFile(fileName, "", TestBucket.BucketId).Result;

		        for (int i = 0; i < parts.Count; i++) {
		            var uploadUrl = Client.LargeFiles.GetUploadPartUrl(start.FileId).Result;
		            var part = Client.LargeFiles.UploadPart(parts[i], i + 1, uploadUrl).Result;
		        }

		        finish = Client.LargeFiles.FinishLargeFile(start.FileId, shas.ToArray()).Result;
		    }
		    catch (Exception e) {
		        Console.WriteLine(e);
		        throw;
		    }
		    finally {
		        // Clean up.
		        FilesToDelete.Add(start);
            }

            
			Assert.AreEqual(start.FileId, finish.FileId, "File Ids did not match.");
		}

	    [TestMethod]
	    public void LargeFileUploadIncompleteGetPartsTest() {
	        var fileName = "B2LargeFileTest.txt";
	        FileStream fileStream = File.OpenRead(Path.Combine(FilePath, fileName));
	        var stream = new StreamReader(fileStream);
	        char[] c = null;
	        List<byte[]> parts = new List<byte[]>();
	        var shas = new List<string>();

	        var listParts = new B2LargeFileParts();

	        while (stream.Peek() >= 0) {
	            c = new char[1024 * (5 * 1024)];
	            stream.Read(c, 0, c.Length);

	            parts.Add(Encoding.UTF8.GetBytes(c));
	        }

	        foreach (var part in parts.Take(2)) {
	            string hash = Utilities.GetSHA1Hash(part);
	            shas.Add(hash);
	        }

	        B2File start = null;
	        B2File finish = null;
	        try {
	            start = Client.LargeFiles.StartLargeFile(fileName, "", TestBucket.BucketId).Result;

	            for (int i = 0; i < 2; i++) {
	                var uploadUrl = Client.LargeFiles.GetUploadPartUrl(start.FileId).Result;
	                var part = Client.LargeFiles.UploadPart(parts[i], i + 1, uploadUrl).Result;
	            }

                // Now we can list parts and get a result
	            listParts = Client.LargeFiles.ListPartsForIncompleteFile(start.FileId, 1, 100).Result;
	        } catch (Exception e) {
	            Console.WriteLine(e);
	            throw;
	        } finally {
	            // Clean up.
	            FilesToDelete.Add(start);
	        }

	        Assert.AreEqual(2, listParts.Parts.Count, "List of parts did not return expected amount of parts.");
	    }

	    [TestMethod]
	    public void LargeFileCancelTest() {
	        var fileName = "B2LargeFileTest.txt";
	        FileStream fileStream = File.OpenRead(Path.Combine(FilePath, fileName));
	        var stream = new StreamReader(fileStream);
	        char[] c = null;
	        List<byte[]> parts = new List<byte[]>();
	        var shas = new List<string>();

	        var cancelledFile = new B2CancelledFile();

	        while (stream.Peek() >= 0) {
	            c = new char[1024 * (5 * 1024)];
	            stream.Read(c, 0, c.Length);

	            parts.Add(Encoding.UTF8.GetBytes(c));
	        }

	        foreach (var part in parts.Take(2)) {
	            string hash = Utilities.GetSHA1Hash(part);
	            shas.Add(hash);
	        }

	        B2File start = null;
	        B2File finish = null;
	        try {
	            start = Client.LargeFiles.StartLargeFile(fileName, "", TestBucket.BucketId).Result;

	            for (int i = 0; i < 2; i++) {
	                var uploadUrl = Client.LargeFiles.GetUploadPartUrl(start.FileId).Result;
	                var part = Client.LargeFiles.UploadPart(parts[i], i + 1, uploadUrl).Result;
	            }

	            // Now we can list parts and get a result
	            cancelledFile = Client.LargeFiles.CancelLargeFile(start.FileId).Result;
	        } catch (Exception e) {
	            Console.WriteLine(e);
	            throw;
	        }

	        Assert.AreEqual(start.FileId, cancelledFile.FileId, "Started file and Cancelled file do not have the same id.");
	    }

	    [TestMethod]
	    public void LargeFileIncompleteListTest() {
	        var fileName = "B2LargeFileTest.txt";
	        FileStream fileStream = File.OpenRead(Path.Combine(FilePath, fileName));
	        var stream = new StreamReader(fileStream);
	        char[] c = null;
	        List<byte[]> parts = new List<byte[]>();
	        var shas = new List<string>();

	        var fileList = new B2IncompleteLargeFiles();

	        while (stream.Peek() >= 0) {
	            c = new char[1024 * (5 * 1024)];
	            stream.Read(c, 0, c.Length);

	            parts.Add(Encoding.UTF8.GetBytes(c));
	        }

	        foreach (var part in parts.Take(2)) {
	            string hash = Utilities.GetSHA1Hash(part);
	            shas.Add(hash);
	        }

	        B2File start = null;
	        B2File finish = null;
	        try {
	            start = Client.LargeFiles.StartLargeFile(fileName, "", TestBucket.BucketId).Result;

	            for (int i = 0; i < 2; i++) {
	                var uploadUrl = Client.LargeFiles.GetUploadPartUrl(start.FileId).Result;
	                var part = Client.LargeFiles.UploadPart(parts[i], i + 1, uploadUrl).Result;
	            }

	            // Now we can list parts and get a result
	            fileList = Client.LargeFiles.ListIncompleteFiles(TestBucket.BucketId).Result;
	        } catch (Exception e) {
	            Console.WriteLine(e);
	            throw;
	        } finally {
	            var cancelledFile = Client.LargeFiles.CancelLargeFile(start.FileId).Result;
	        }

	        Assert.AreEqual(1, fileList.Files.Count, "Incomplete file list count does not match what we expected.");
	    }

        [TestCleanup]
		public void Cleanup() {
			foreach (B2File b2File in FilesToDelete) {
				var deletedFile = Client.Files.Delete(b2File.FileId, b2File.FileName).Result;
            }
			var deletedBucket = Client.Buckets.Delete(TestBucket.BucketId).Result;
		}
	}
}
