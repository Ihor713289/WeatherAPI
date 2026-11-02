    workspace "InhollandSAT" ""   {
    
        
    !identifiers hierarchical
    model   {
        
        
        user   =   Person "user"
        group "InhollandSat"   {
            
            
        SCS   = softwareSystem "Satellite Controll System"  {
                

                dashboard   = container "Dashboard"  {
                    

                
                }
                backend   = container "Backend"  {
                    
                    
                commands   = component "/newCommand"
                images   = component "/getimages"
                tel   = component  "/gettelemetry"
                tunnel   = component "/pushdata"
            
                
                }
            storage   = container "Azure storage"   {
                    
                    
            blob   = component "Blob storage"
            queue   = component "Queue storage"

                
                }
        
            
            }

        
        }

        ground_station   = softwareSystem "Ground station"  {
            tunnel   = container "Wireguard tunnel"
            radio   = container "radio communications"
        }       
        satellite   = SoftwareSystem "Satellite"  {
            
            sband   = container "sband downlink"
            vhf   = container "vhf uplink/downlink"
            VIMMI   = container "camera installation"
    
            OBC   = container "on board computer"  {

                smt32   = component "main processor"
                sd   = component "storage"
                 
                }

        }
        satellite.sband   ->   ground_station.radio "send payload data"
        satellite.vhf   ->   ground_station.radio "send satellite telemetry & recieve commands"
        satellite.VIMMI   ->   satellite.OBC.sd "store raw image"
        ground_station.radio -> satellite.vhf "transfer satelite commands"
        


        SCS.dashboard   ->   SCS.backend.commands "POST commands"
        SCS.dashboard   ->   SCS.backend.images "GET images"
        SCS.dashboard   ->   SCS.backend.tel "GET telemetry data"
        SCS.backend   ->   SCS.storage.blob "store images"    
        SCS.backend   ->   SCS.storage.queue "store commands"
        user   ->   SCS.dashboard "Uses"
        ground_station.tunnel   ->   SCS.backend.tunnel "push satellite data"
        SCS.backend   ->   ground_station.tunnel "send satellite commands"

        
        

        
    
    }
        views   {
        
        
                    properties   {
            
            
            "structurizr.sort" "created"
        
        
        }
            styles   {
            
            
             element "Person"  {
                
                
             shape "person"
             
            
            }
            
        
        }
        
    
    }
        

    
}


    
