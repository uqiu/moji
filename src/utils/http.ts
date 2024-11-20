
import axios from 'axios';
import { ConfigLoader } from '../config/configLoader';

const configLoader = ConfigLoader.getInstance();
const httpConfig = configLoader.getConfig();

const httpClient = axios.create(httpConfig);

export default httpClient;