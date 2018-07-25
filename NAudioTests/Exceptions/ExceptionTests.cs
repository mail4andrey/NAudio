using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Moq;
using NAudio;
using NAudio.Wave;
using NAudio.Wave.Compression;
using NUnit.Framework;
using Telerik.JustMock;

namespace NAudioTests.Exceptions
{
	[TestFixture]
	public class ExceptionTests
	{
		[Test]
		[Category("IntegrationTest")]
		public void AcmStreamClose_ThrowAccessViolationException()
		{
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			// in real situation when execute AcmInterop.acmStreamClose
			// field streamHandle become wrong (InvalidHandle) and throw exception AccessViolationException
			// Setup execute acmStreamClose to exception 
			Telerik.JustMock.Mock.Arrange(() => AcmInterop.acmStreamClose(Arg.IsAny<IntPtr>(), Arg.IsAny<int>()))
				.DoInstead((IntPtr hAcmStream, int closeFlags) => throw new AccessViolationException("Something wrong"));

			string testDataFolder = @"C:\R";
			//string testDataFolder = @"C:\Users\Mark\Downloads\NAudio";
			if (!Directory.Exists(testDataFolder))
			{
				Assert.Ignore("{0} not found", testDataFolder);
			}

			foreach (string file in Directory.GetFiles(testDataFolder, "*.mp3"))
			{
				string mp3File = Path.Combine(testDataFolder, file);
				Debug.WriteLine($"Opening {mp3File}");

				var naudio = new NAudioWrapper();
				var duration = naudio.GetDuration(mp3File);
				Debug.WriteLine($"Duration: {duration}");
			}

			// force run GC and wait UnhandledException from GC
			GC.Collect();
			GC.WaitForPendingFinalizers();
			// after AccessViolationException in ~AcmStream we have MmException because streamHandle is invalid handle
			// don't know how to code this situation whiout changing AcmStream.Dispose(bool)
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Debug.WriteLine("UnhandledException: " + e);
		}

		public class NAudioWrapper
		{
			[HandleProcessCorruptedStateExceptions]
			public TimeSpan? GetDuration(string fileName)
			{
				IDisposable disposable = null;
				try
				{
					var mp3FileReader = new Mp3FileReader(fileName);
					disposable = mp3FileReader;
					using (mp3FileReader)
					{
						return mp3FileReader.TotalTime;
					}
				}
				catch (AccessViolationException accessViolationException)
				{
					Debug.WriteLine(accessViolationException);

					try
					{
						//try redispose after exception
						disposable?.Dispose();
					}
					catch (MmException mmException)
					{
						Debug.WriteLine(mmException);
					}

					return null;
				}
			}
		}
	}
}
