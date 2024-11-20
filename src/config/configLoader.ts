
import fs from 'fs';
import path from 'path';

export class ConfigLoader {
  private static instance: ConfigLoader;
  private config: any;

  private constructor() {
    this.loadConfig();
  }

  public static getInstance(): ConfigLoader {
    if (!ConfigLoader.instance) {
      ConfigLoader.instance = new ConfigLoader();
    }
    return ConfigLoader.instance;
  }

  private loadConfig(): void {
    try {
      const configPath = path.resolve(__dirname, '../../config/http.config.json');
      const configData = fs.readFileSync(configPath, 'utf-8');
      this.config = JSON.parse(configData);
    } catch (error) {
      console.error('加载配置文件失败:', error);
      throw error;
    }
  }

  public getConfig(): any {
    return this.config;
  }

  public reloadConfig(): void {
    this.loadConfig();
  }
}