import './DataCredit.css'

// Every district boundary on this site comes from the Census Bureau's TIGER
// data, and the site says so wherever the map is offered or drawn: Home and the
// result page's district list.

const CENSUS_URL = 'https://tigerweb.geo.census.gov/tigerwebmain/TIGERweb_main.html'

export function DataCredit({ className }: { className: string }) {
  return (
    <p className={`data-credit ${className}`}>
      Map data:{' '}
      {/* noreferrer, because the result page's URL holds the visitor's address. */}
      <a href={CENSUS_URL} rel="noreferrer">
        U.S. Census Bureau
      </a>
    </p>
  )
}
