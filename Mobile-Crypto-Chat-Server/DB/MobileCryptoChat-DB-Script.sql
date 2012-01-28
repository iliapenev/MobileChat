USE [MobileCryptoChat]
GO
/****** Object:  Table [dbo].[Users]    Script Date: 12/22/2011 17:23:08 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[Users](
	[UserId] [int] IDENTITY(1,1) NOT NULL,
	[MSISDN] [varchar](30) NOT NULL,
	[AuthCodeSHA1] [char](40) NOT NULL,
	[SessionKey] [varchar](50) NULL,
	[LastActivity] [datetime] NULL,
 CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING OFF
GO
CREATE UNIQUE NONCLUSTERED INDEX [UK_Users] ON [dbo].[Users] 
(
	[MSISDN] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
GO
SET IDENTITY_INSERT [dbo].[Users] ON
INSERT [dbo].[Users] ([UserId], [MSISDN], [AuthCodeSHA1], [SessionKey], [LastActivity]) VALUES (156, N'+359899888777', N'8bcac5dabf06219843a5a3b755c47e69600e050a', NULL, CAST(0x00009FC1011CCB9C AS DateTime))
INSERT [dbo].[Users] ([UserId], [MSISDN], [AuthCodeSHA1], [SessionKey], [LastActivity]) VALUES (205, N'+359888123456', N'8bcac5dabf06219843a5a3b755c47e69600e050a', NULL, CAST(0x00009FC1011CCBBB AS DateTime))
SET IDENTITY_INSERT [dbo].[Users] OFF
/****** Object:  Table [dbo].[Messages]    Script Date: 12/22/2011 17:23:08 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[Messages](
	[MessageId] [int] IDENTITY(1,1) NOT NULL,
	[FromUserId] [int] NOT NULL,
	[ToUserId] [int] NOT NULL,
	[MsgType] [varchar](20) NOT NULL,
	[MsgDate] [datetime] NOT NULL,
	[MsgText] [varchar](max) NOT NULL,
 CONSTRAINT [PK_Messages] PRIMARY KEY CLUSTERED 
(
	[MessageId] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING OFF
GO
CREATE NONCLUSTERED INDEX [IX_Messages_Date] ON [dbo].[Messages] 
(
	[MsgDate] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Messages_ToUser] ON [dbo].[Messages] 
(
	[ToUserId] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ChatSessions]    Script Date: 12/22/2011 17:23:08 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[ChatSessions](
	[ChatSessionId] [int] IDENTITY(1,1) NOT NULL,
	[FromUserId] [int] NOT NULL,
	[ToUserId] [int] NOT NULL,
	[ChatState] [varchar](20) NOT NULL,
 CONSTRAINT [PK_ChatSessions] PRIMARY KEY CLUSTERED 
(
	[ChatSessionId] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING OFF
GO
CREATE NONCLUSTERED INDEX [IX_ChatSessions_FromUser] ON [dbo].[ChatSessions] 
(
	[FromUserId] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
GO
/****** Object:  ForeignKey [FK_Messages_Users_From]    Script Date: 12/22/2011 17:23:08 ******/
ALTER TABLE [dbo].[Messages]  WITH CHECK ADD  CONSTRAINT [FK_Messages_Users_From] FOREIGN KEY([FromUserId])
REFERENCES [dbo].[Users] ([UserId])
GO
ALTER TABLE [dbo].[Messages] CHECK CONSTRAINT [FK_Messages_Users_From]
GO
/****** Object:  ForeignKey [FK_Messages_Users_To]    Script Date: 12/22/2011 17:23:08 ******/
ALTER TABLE [dbo].[Messages]  WITH CHECK ADD  CONSTRAINT [FK_Messages_Users_To] FOREIGN KEY([ToUserId])
REFERENCES [dbo].[Users] ([UserId])
GO
ALTER TABLE [dbo].[Messages] CHECK CONSTRAINT [FK_Messages_Users_To]
GO
/****** Object:  ForeignKey [FK_ChatSessions_Users_From]    Script Date: 12/22/2011 17:23:08 ******/
ALTER TABLE [dbo].[ChatSessions]  WITH CHECK ADD  CONSTRAINT [FK_ChatSessions_Users_From] FOREIGN KEY([FromUserId])
REFERENCES [dbo].[Users] ([UserId])
GO
ALTER TABLE [dbo].[ChatSessions] CHECK CONSTRAINT [FK_ChatSessions_Users_From]
GO
/****** Object:  ForeignKey [FK_ChatSessions_Users_To]    Script Date: 12/22/2011 17:23:08 ******/
ALTER TABLE [dbo].[ChatSessions]  WITH CHECK ADD  CONSTRAINT [FK_ChatSessions_Users_To] FOREIGN KEY([ToUserId])
REFERENCES [dbo].[Users] ([UserId])
GO
ALTER TABLE [dbo].[ChatSessions] CHECK CONSTRAINT [FK_ChatSessions_Users_To]
GO
