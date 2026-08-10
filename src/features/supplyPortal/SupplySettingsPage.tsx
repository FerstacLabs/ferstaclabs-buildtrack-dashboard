import { SafetyCertificateOutlined } from '@ant-design/icons'
import { Alert, Card, Descriptions, Tag } from 'antd'

export const SupplySettingsPage = () => (
  <div className="field-page supply-page">
    <div className="field-toolbar">
      <div>
        <span className="field-eyebrow">Supply Portal</span>
        <h2>Ayarlar</h2>
      </div>
    </div>
    <Alert
      type="info"
      showIcon
      className="field-alert"
      message="Demo ayarı"
      description="Bu portal 1C inteqrasiyası hazır olana qədər BuildTrack backendində sübutlu satınalma və anbar rezervasiya axınını idarə edir."
    />
    <Card className="soft-card">
      <Descriptions column={1} bordered size="small">
        <Descriptions.Item label="Sübut siyasəti"><Tag color="orange"><SafetyCertificateOutlined /> Çek + məhsul şəkli məcburidir</Tag></Descriptions.Item>
        <Descriptions.Item label="Qiymət görünüşü">Qiymət məlumatı yalnız supply və management rollarında görünür.</Descriptions.Item>
        <Descriptions.Item label="Prorab məxfiliyi">Prorab stok qalığını, anbar mövcudluğunu və alış qiymətlərini görmür.</Descriptions.Item>
        <Descriptions.Item label="Gələcək inteqrasiya">1C stok, supplier və mal qəbulu sənədləri ilə sinxronizasiya üçün hazır API strukturu.</Descriptions.Item>
      </Descriptions>
    </Card>
  </div>
)
