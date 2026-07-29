import { Select } from 'antd'

export const constructionUnitOptions = [
  'iş',
  'm',
  'm²',
  'm3',
  'm³',
  'sm',
  'mm',
  'ədəd',
  'dəst',
  'ton',
  'kq',
  'litr',
  'm²/gün',
  'm³/gün',
  'saat',
  'gün',
  'ay',
  'maşın',
  'reys',
  'kub',
  'paqon metr',
  'kv.m',
  'kub.m',
  'ha',
  'sot',
  'rulon',
  'kisə',
  'bağlama',
  'qutu',
  'konteyner',
  'komplekt',
].map((unit) => ({ value: unit, label: unit }))

interface UnitSelectProps {
  value?: string
  onChange?: (value: string) => void
  placeholder?: string
}

export const UnitSelect = ({ onChange, placeholder = 'Vahid seçin və ya yazın', value }: UnitSelectProps) => (
  <Select
    allowClear
    showSearch
    mode="tags"
    maxCount={1}
    value={value ? [value] : []}
    placeholder={placeholder}
    options={constructionUnitOptions}
    onChange={(values) => onChange?.(values[values.length - 1] ?? '')}
    filterOption={(input, option) => String(option?.label ?? '').toLowerCase().includes(input.toLowerCase())}
  />
)
