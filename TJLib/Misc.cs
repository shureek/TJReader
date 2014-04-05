/*
 * Created by SharpDevelop.
 * User: Kuzin.A
 * Date: 03.04.2014
 * Time: 18:21
 * 
 */

using System;

namespace TJLib
{
	public class ErrorEventArgs : EventArgs 
	{
		public Exception Error { get; private set; }
		
		public ErrorEventArgs(Exception error)
		{
			this.Error = error;
		}
	}
	
	public enum ParserState
	{
		Begin = 0,
		BefereEventType,
		EventType,
		BeforeLevel,
		Level,
		BeforeProperty,
		PropertyName,
		EqualSign,
		PropertyValue,
		AfterProperty,
		End,
		Unknown = -1,
		Error = -2
	}
	
	enum LexemType
	{
		None = 0,
		Word,
		String,
		Comma,
		EqualSign,
		NewLine
	}
}