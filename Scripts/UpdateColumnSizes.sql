-- Update MapObjects table columns to support longer generated content
ALTER TABLE [MapObjects] ALTER COLUMN [GeneratedDescription] nvarchar(2000) NULL;
ALTER TABLE [MapObjects] ALTER COLUMN [GeneratedStory] nvarchar(2000) NULL;
ALTER TABLE [MapObjects] ALTER COLUMN [GeneratedFacts] nvarchar(2000) NULL;

-- Update InteractionLogs table columns to support longer LLM content
ALTER TABLE [InteractionLogs] ALTER COLUMN [LLMPrompt] nvarchar(2000) NULL;
ALTER TABLE [InteractionLogs] ALTER COLUMN [LLMResponse] nvarchar(2000) NULL;
