﻿ 
namespace StarTrinity.CST
{	
		public static class CompilationInfo
		{
			public static uint CompilationDateTimeUtc_uint32
			{
				get
				{
					return 42816990;
				}
			}
			public static System.DateTime CompilationDateTimeUtc
			{
				get
				{
					return Dcomms.MiscProcedures.Uint32secondsToDateTime(CompilationDateTimeUtc_uint32);
				}
			}
			public static string CompilationDateTimeUtcStr
			{
				get
				{
					return string.Format("{0:yyMMdd-HH:mm}", CompilationDateTimeUtc);
				}
			}
		}
}
