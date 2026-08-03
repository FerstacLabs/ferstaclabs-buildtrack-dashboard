const marketingBaseUrl = (import.meta.env.VITE_MARKETING_BASE_URL as string | undefined) ?? 'https://buildtrack.ferstaclabs.com'

export const AuthLandingLink = () => (
  <a className="auth-brand-link" href={marketingBaseUrl}>
    <span className="auth-brand-logo">BT</span>
    <span>← BuildTrack səhifəsinə qayıt</span>
  </a>
)
