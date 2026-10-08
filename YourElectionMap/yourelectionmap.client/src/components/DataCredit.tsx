import './DataCredit.css'

// Every district boundary on this site comes from the Redistricting Data Hub,
// and the site has to say so wherever the map is offered or drawn: Home and the
// result page's district list.

const DATA_HUB_URL = 'https://redistrictingdatahub.org/'

export function DataCredit({ className }: { className: string }) {
  return (
    <p className={`data-credit ${className}`}>
      Map data:{' '}
      {/* noreferrer, because the result page's URL holds the visitor's address. */}
      <a href={DATA_HUB_URL} rel="noreferrer">
        Redistricting Data Hub
      </a>
    </p>
  )
}
